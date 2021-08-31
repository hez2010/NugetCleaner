using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using Spectre.Console;

static long CalculateDirectorySize(string directory)
{
    var size = 0L;
    foreach (var fileInfo in new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories))
    {
        size += fileInfo.Length;
    }

    return size;
}

var packages = new List<PackageDescription>();
var totalSavedSize = 0L;

AnsiConsole.Status()
    .Start("Welcome", ctx =>
    {
        ctx.Status("Finding local nuget packages directory...");

        var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        if (!Directory.Exists(nugetRoot))
        {
            AnsiConsole.WriteLine("[ERROR] Cannot find nuget packages directory.");
            return;
        }

        ctx.Status("Gathering information for local nuget packages...");

        foreach (var packageRoot in Directory.EnumerateDirectories(nugetRoot))
        {
            var name = Path.GetFileName(packageRoot);
            ctx.Status($"Gathering information for {name}...");
            AnsiConsole.WriteLine($"[LOG] Package: {name}");

            var description = new PackageDescription(packageRoot, new());
            var versions = Directory.GetDirectories(packageRoot)
                .Select(i => (Version: new NuGetVersion(Path.GetFileName(i)), Path: i))
                .OrderByDescending(i => i);

            ctx.Status($"Cleaning up {name}...");

            var lastMajorVersion = -1;
            foreach (var version in versions)
            {
                if (version.Version.Major != lastMajorVersion)
                {
                    description.Versions.Add(new(version.Version, PackageAction.Keep));
                    lastMajorVersion = version.Version.Major;
                    AnsiConsole.WriteLine($"      -> Keep: {version.Version}");
                    continue;
                }

                description.Versions.Add(new(version.Version, PackageAction.Drop));
                AnsiConsole.WriteLine($"      -> Drop: {version.Version}");
                totalSavedSize += CalculateDirectorySize(version.Path);
                Directory.Delete(version.Path, true);
            }

            packages.Add(description);

            // TODO: handle prerelease and non-prerelease, add confirmation prompt and etc.
        }
    });

AnsiConsole.WriteLine($"Saved {totalSavedSize / 1048576F} Mb in total.");

enum PackageAction { Keep, Drop }
record class PackageVersionDescription(NuGetVersion Version, PackageAction Action);
record class PackageDescription(string Name, List<PackageVersionDescription> Versions);
