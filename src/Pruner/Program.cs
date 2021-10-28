using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniCover.Core.Instrumentation;
using MiniCover.Core.Model;
using Newtonsoft.Json;

namespace Pruner.Instrumenter
{
    class PrunerSettings
    {
        public TestProvider[]? Providers { get; set; }
    }

    class TestProvider
    {
        public string WorkingDirectory { get; set; } = null!;
        public string Id { get; set; } = null!;
    }
    
    class Program
    {
        private readonly IFileSystem fileSystem;
        private readonly IInstrumenter instrumenter;

        public Program(
            IFileSystem fileSystem,
            IInstrumenter instrumenter)
        {
            this.fileSystem = fileSystem;
            this.instrumenter = instrumenter;
        }

        private async Task RunAsync(string[] args)
        {
            var settingsId = args.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(settingsId))
                throw new ValidationException("Settings ID not specified.");

            var prunerPath = Path.Combine(
                Environment.CurrentDirectory,
                ".pruner");

            var settingsContents = await File.ReadAllTextAsync(Path.Combine(
                prunerPath,
                "settings.json"));
            var settings = JsonConvert.DeserializeObject<PrunerSettings>(settingsContents);
            var dotnetSettings = settings.Providers?.SingleOrDefault(x => x.Id == settingsId);
            if (dotnetSettings == null)
                throw new ValidationException($"The Pruner settings file did not contain a settings object for ID {settingsId}");

            var providerWorkingDirectoryPath = fileSystem.DirectoryInfo.FromDirectoryName(Path.Combine(
                Environment.CurrentDirectory,
                dotnetSettings.WorkingDirectory));
            var assemblies = GetFiles(
                new[]
                {
                    "**/*.dll"
                },
                new[]
                {
                    "**/obj/**/*.dll"
                },
                providerWorkingDirectoryPath);
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
                providerWorkingDirectoryPath);
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
                providerWorkingDirectoryPath);

            var temporarySettingsDirectory = Path.Combine(
                prunerPath,
                "temp",
                settingsId);
            var instrumentationContext = new FileBasedInstrumentationContext
            {
                Assemblies = assemblies,
                HitsPath = Path.Combine(
                    temporarySettingsDirectory,
                    "hits.tmp"),
                Sources = sourceFiles,
                Tests = testFiles,
                Workdir = providerWorkingDirectoryPath
            };

            var result = instrumenter.Instrument(instrumentationContext);
            SaveCoverageFile(
                fileSystem.FileInfo.FromFileName(
                    Path.Combine(
                        temporarySettingsDirectory,
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
                        Path.GetFullPath(Path.Combine(parentDir.ToString(), f.Path))))
                .ToArray();
        }

        private static void SaveCoverageFile(IFileInfo coverageFile, InstrumentationResult result)
        {
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
            var json = JsonConvert.SerializeObject(result, Formatting.Indented, settings);
            File.WriteAllText(coverageFile.FullName, json);
        }

        static async Task Main(string[] args)
        {
            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddMiniCoverCore();
                serviceCollection.AddLogging();
                
                serviceCollection.AddTransient<IFileSystem, FileSystem>();
                serviceCollection.AddTransient<Program>();

                await using var serviceProvider = serviceCollection.BuildServiceProvider();

                var program = serviceProvider.GetRequiredService<Program>();
                await program.RunAsync(args);
            }
            catch (ValidationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                await Console.Error.WriteLineAsync(ex.Message);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Error.WriteLineAsync(ex.ToString());
            }
        }
    }
}