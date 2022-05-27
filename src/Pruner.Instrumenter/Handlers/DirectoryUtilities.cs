using System.IO;
using System.IO.Abstractions;

namespace Pruner.Instrumenter.Handlers;

public static class DirectoryUtilities
{
    public static string GetCoverageJsonFilePathFromSettingsDirectory(IDirectoryInfo settingsDirectory)
    {
        return Path.Combine(
            settingsDirectory.FullName,
            "coverage.json");
    }
}