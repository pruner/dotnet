using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using MiniCover.Core.Instrumentation;
using MiniCover.Core.Model;
using Newtonsoft.Json;

namespace Pruner.Instrumenter.Handlers
{
    public class InstrumentCommandHandler : ICommandHandler
    {
        private readonly IFileSystem fileSystem;
        private readonly IInstrumenter instrumenter;

        public InstrumentCommandHandler(
            IFileSystem fileSystem,
            IInstrumenter instrumenter)
        {
            this.fileSystem = fileSystem;
            this.instrumenter = instrumenter;
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
                    "**/*.dll"
                },
                new[]
                {
                    "**/obj/**/*.dll"
                },
                workingDirectory);
            if (assemblies.Length == 0)
                throw new ValidationException("No assemblies found");

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
                throw new ValidationException("No source files found");

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

            var instrumentationContext = new FileBasedInstrumentationContext
            {
                Assemblies = assemblies,
                HitsPath = Path.Combine(
                    settingsDirectory.FullName,
                    "hits.tmp"),
                Sources = sourceFiles,
                Tests = testFiles,
                Workdir = workingDirectory
            };

            var result = instrumenter.Instrument(instrumentationContext);
            await SaveCoverageFileAsync(
                fileSystem.FileInfo.FromFileName(
                    Path.Combine(
                        settingsDirectory.FullName,
                        "coverage.tmp")), 
                result);
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

        private static async Task SaveCoverageFileAsync(IFileInfo coverageFile, InstrumentationResult result)
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
        }
    }
}