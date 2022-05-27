using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiniCover.Core.Instrumentation;
using MiniCover.Core.Model;
using Newtonsoft.Json;

namespace Pruner.Instrumenter.Handlers
{
    public class InstrumentCommandHandler : ICommandHandler
    {
        private readonly IFileSystem _fileSystem;
        private readonly IInstrumenter _instrumenter;
        private readonly ILogger<InstrumentCommandHandler> _logger;

        public InstrumentCommandHandler(
            IFileSystem fileSystem,
            IInstrumenter instrumenter,
            ILogger<InstrumentCommandHandler> logger)
        {
            _fileSystem = fileSystem;
            _instrumenter = instrumenter;
            _logger = logger;
        }
        
        public bool CanHandle(Command command)
        {
            return command == Command.Instrument;
        }

        public async Task HandleAsync(
            IDirectoryInfo workingDirectory,
            IDirectoryInfo settingsDirectory)
        {
            var assemblies = GetFiles(
                new[]
                {
                    ".pruner-bin/**/*.dll"
                },
                new[]
                {
                    "**/obj/**/*.dll"
                },
                workingDirectory);
            if (assemblies.Length == 0)
                throw new ValidationException($"No assemblies found from directory {workingDirectory.FullName}");

            var sourceFiles = GetFiles(
                new[]
                {
                    "**/*.cs"
                },
                new[]
                {
                    "**/bin/**/*.cs",
                    "**/obj/**/*.cs",
                    "tests/**/*.cs",
                    "test/**/*.cs",
                    "**/*.Tests/**/*.cs"
                },
                workingDirectory);
            if (sourceFiles.Length == 0)
                throw new ValidationException($"No source files found from directory {workingDirectory.FullName}");

            var testFiles = GetFiles(
                new[]
                {
                    "tests/**/*.cs",
                    "test/**/*.cs",
                    "**/*.Tests/**/*.cs"
                },
                new[]
                {
                    "**/bin/**/*.cs",
                    "**/obj/**/*.cs"
                },
                workingDirectory);

            DebugFileCollection("Assemblies", assemblies);
            DebugFileCollection("Source files", sourceFiles);
            DebugFileCollection("Test files", testFiles);

            var instrumentationContext = new FileBasedInstrumentationContext
            {
                Assemblies = assemblies,
                HitsPath = EnsureCleanHitsDirectoryPath(settingsDirectory),
                Sources = sourceFiles,
                Tests = testFiles,
                Workdir = workingDirectory
            };

            var result = _instrumenter.Instrument(instrumentationContext);
            await SaveCoverageFileAsync(
                _fileSystem.FileInfo.FromFileName(
                    DirectoryUtilities.GetCoverageJsonFilePathFromSettingsDirectory(settingsDirectory)), 
                result);
        }

        private static string EnsureCleanHitsDirectoryPath(IDirectoryInfo settingsDirectory)
        {
            var hitsDirectoryPath = Path.Combine(
                settingsDirectory.FullName,
                "hits");
            
            if (Directory.Exists(hitsDirectoryPath))
            {
                Directory.Delete(hitsDirectoryPath, true);
            }
            
            Directory.CreateDirectory(hitsDirectoryPath);
            
            return hitsDirectoryPath;
        }

        private void DebugFileCollection(string name, IEnumerable<IFileInfo> files)
        {
            _logger.LogInformation(
                "Files {FileHeader}: {Files}", 
                name, 
                string.Join(", ", files.Select(f => f.FullName)));
        }

        private static IFileInfo[] GetFiles(
            IEnumerable<string> includes,
            IEnumerable<string> excludes,
            IDirectoryInfo parentDir)
        {
            var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();

            foreach (var include in includes)
            {
                matcher.AddInclude(include);
            }

            foreach (var exclude in excludes)
            {
                matcher.AddExclude(exclude);
            }

            var fileMatchResult =
                matcher.Execute(
                    new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(
                        new DirectoryInfo(parentDir.FullName)));

            return fileMatchResult.Files
                .Select(f =>
                    parentDir.FileSystem.FileInfo.FromFileName(
                        Path.GetFullPath(Path.Combine(
                            parentDir?.ToString() ?? throw new InvalidOperationException("Could not find path."), 
                            f.Path))))
                .ToArray();
        }

        private async Task SaveCoverageFileAsync(IFileInfo coverageFile, InstrumentationResult result)
        {
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
            var json = JsonConvert.SerializeObject(result, Formatting.Indented, settings);

            Directory.CreateDirectory(
                Path.GetDirectoryName(coverageFile.FullName) ??
                throw new InvalidOperationException("Can't find directory name."));
            await File.WriteAllTextAsync(coverageFile.FullName, json);
            
            _logger.LogInformation("Saved coverage file to: {CoverageFilePath}", coverageFile.FullName);
        }
    }
}