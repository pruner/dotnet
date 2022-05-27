using System.Collections.Generic;

// ReSharper disable CollectionNeverQueried.Global

namespace Pruner.Instrumenter.Models
{
    internal class StateFileCoverage
    {
        public string Path { get; set; } = null!;
        public HashSet<long> LineCoverage { get; set; } = new();
    }
}