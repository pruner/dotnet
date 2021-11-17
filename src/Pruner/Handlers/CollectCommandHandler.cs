using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MiniCover.Core.Hits;
using MiniCover.Core.Model;
using MiniCover.HitServices;
using Newtonsoft.Json;

namespace Pruner.Instrumenter.Handlers
{
    public class CollectCommandHandler : ICommandHandler
    {
        private readonly IHitsReader _hitsReader;

        public CollectCommandHandler(
            IHitsReader hitsReader)
        {
            _hitsReader = hitsReader;
        }
        
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

            foreach (var sourceFile in sourceFiles)
            {
                //https://github.com/lucaslorentz/minicover/blob/master/src/MiniCover.Reports/Html/HtmlReport.cs
                //https://github.com/lucaslorentz/minicover/blob/master/src/MiniCover.Reports/Html/HtmlSourceFileReport.cs
            }
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