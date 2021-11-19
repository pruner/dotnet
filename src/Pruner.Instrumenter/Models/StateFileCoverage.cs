using System.Collections.Generic;

namespace Pruner.Models
{
    internal class StateFileCoverage
    {
        public string Path { get; set; }
        public HashSet<long> LineCoverage { get; set; } = new();
    }
}