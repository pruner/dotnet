using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiniCover.Core.Hits;
using MiniCover.Core.Model;
using MiniCover.HitServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Pruner.Instrumenter.Models;

namespace Pruner.Instrumenter.Handlers
{
    public class CollectCommandHandler : ICommandHandler
    {
        private readonly ILogger<CollectCommandHandler> _logger;
        private readonly IHitsReader _hitsReader;

        public CollectCommandHandler(
            ILogger<CollectCommandHandler> logger,
            IHitsReader hitsReader)
        {
            _logger = logger;
            _hitsReader = hitsReader;
        }
        
        public bool CanHandle(Command command)
        {
            return command == Command.Collect;
        }

        public async Task HandleAsync(
            TestProvider provider,
            IDirectoryInfo settingsDirectory)
        {
            _logger.LogInformation("Collecting coverage");
            
            var state = GatherStateFromCoverage(provider, settingsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(
                    settingsDirectory.FullName,
                    "state.json"),
                JsonConvert.SerializeObject(
                    state,
                    new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }));
        }

        private State GatherStateFromCoverage(
            TestProvider provider,
            IDirectoryInfo settingsDirectory)
        {
            var hitsInfo = _hitsReader.TryReadFromDirectory(
                Path.Combine(settingsDirectory.FullName, "hits"));
            var sourceFiles = GetSourceFiles(settingsDirectory);

            var state = new State();
            foreach (var sourceFile in sourceFiles)
            {
                _logger.LogInformation("Collecting coverage for {SourceFilePath}", sourceFile.Path);

                foreach (var sequence in sourceFile.Sequences)
                {
                    var contexts = hitsInfo.GetHitContexts(sequence.HitId);
                    foreach (var context in contexts)
                    {
                        var testState = AddOrUpdateTestCoverage(context, state);

                        var fileCoverage = AddOrUpdateFileCoverage(
                            provider,
                            testState, 
                            sourceFile);

                        var lineHitCount = context.GetHitCount(sequence.HitId);
                        if (lineHitCount > 0)
                        {
                            AddSequenceLinesToCoverage(
                                sequence, 
                                fileCoverage);
                        }
                    }
                }
            }

            return state;
        }

        private static StateTest AddOrUpdateTestCoverage(HitContext context, State state)
        {
            var contextName = $"{context.ClassName}.{context.MethodName}";

            var testState =
                state.Tests.SingleOrDefault(x => x.Name == contextName) ??
                new StateTest();
            testState.Name = contextName;

            state.Tests.Add(testState);
            return testState;
        }

        private static StateFileCoverage AddOrUpdateFileCoverage(TestProvider provider, StateTest testState,
            SourceFile sourceFile)
        {
            var sanitizedFilePath = SanitizeFilePath(
                Path.Combine(
                    provider.WorkingDirectory,
                    sourceFile.Path));
            var fileCoverage =
                testState.FileCoverage.SingleOrDefault(x => x.Path == sanitizedFilePath) ??
                new StateFileCoverage();
            fileCoverage.Path = sanitizedFilePath;

            testState.FileCoverage.Add(fileCoverage);
            return fileCoverage;
        }

        private static void AddSequenceLinesToCoverage(InstrumentedSequence sequence, StateFileCoverage fileCoverage)
        {
            for (var lineIndex = Math.Max(0, sequence.StartLine - 1);
                 lineIndex <= sequence.EndLine + 1;
                 ++lineIndex)
            {
                fileCoverage.LineCoverage.Add(lineIndex);
            }
        }

        private static string SanitizeFilePath(string path)
        {
            return path.Replace("\\", "/");
        }

        private static SourceFile[] GetSourceFiles(IDirectoryInfo settingsDirectory)
        {
            var instrumentationResult = JsonConvert.DeserializeObject<InstrumentationResult>(
                File.ReadAllText(
                    DirectoryUtilities.GetCoverageJsonFilePathFromSettingsDirectory(settingsDirectory)));
            if (instrumentationResult == null)
                throw new InvalidOperationException("Couldn't find instrumentation result.");

            var sourceFiles = instrumentationResult.GetSourceFiles();
            return sourceFiles;
        }
    }
}