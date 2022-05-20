using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using MiniCover.Core.Hits;
using MiniCover.Core.Model;
using MiniCover.HitServices;
using Newtonsoft.Json;
using Pruner.Models;

namespace Pruner.Instrumenter.Handlers
{
    public class CollectCommandHandler : ICommandHandler
    {
        public bool CanHandle(Command command)
        {
            return command == Command.Collect;
        }

        public async Task HandleAsync(
            IDirectoryInfo workingDirectory,
            IDirectoryInfo settingsDirectory)
        {
            var hitsInfo = GetHitsInfo(settingsDirectory);
            var sourceFiles = GetSourceFiles(settingsDirectory);

            var state = new State();
            foreach (var sourceFile in sourceFiles)
            {
                Console.WriteLine($"Collecting coverage for {sourceFile}");
                
                foreach (var sequence in sourceFile.Sequences)
                {
                    var contexts = hitsInfo.GetHitContexts(sequence.HitId);
                    foreach (var context in contexts)
                    {
                        var contextName = $"{context.ClassName}.{context.MethodName}";

                        var testState = 
                            state.Tests.SingleOrDefault(x => x.Name == contextName) ??
                            new StateTest();
                        testState.Name = contextName;
                        
                        state.Tests.Add(testState);

                        var fileCoverage =
                            testState.FileCoverage.SingleOrDefault(x => x.Path == sourceFile.Path) ??
                            new StateFileCoverage();
                        fileCoverage.Path = sourceFile.Path;

                        testState.FileCoverage.Add(fileCoverage);
                        
                        foreach (var line in sequence.GetLines())
                        {
                            fileCoverage.LineCoverage.Add(line);
                        }
                    }
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(
                    settingsDirectory.FullName,
                    "state.json"),
                JsonConvert.SerializeObject(state));
        }

        private static SourceFile[] GetSourceFiles(IDirectoryInfo settingsDirectory)
        {
            var instrumentationResult = JsonConvert.DeserializeObject<InstrumentationResult>(
                File.ReadAllText(
                    Path.Combine(
                        settingsDirectory.FullName,
                        "coverage.tmp")));
            if (instrumentationResult == null)
                throw new InvalidOperationException("Couldn't find instrumentation result.");

            var sourceFiles = instrumentationResult.GetSourceFiles();
            return sourceFiles;
        }

        private HitsInfo GetHitsInfo(
            IDirectoryInfo settingsDirectory)
        {
            var contexts = new HashSet<HitContext>();
            if (!settingsDirectory.Exists)
                return new HitsInfo(contexts);

            foreach (var hitFile in settingsDirectory.GetFiles("*.hits", SearchOption.AllDirectories))
            {
                using var fileStream = hitFile.Open(FileMode.Open, FileAccess.Read);
                var hitContexts = HitContext.Deserialize(fileStream);
                foreach (var hitContext in hitContexts)
                {
                    contexts.Add(hitContext);
                }
            }

            return new HitsInfo(contexts);
        }
    }
}