using System.Collections.Generic;

namespace Pruner.Instrumenter.Models
{
    class StateTest
    {
        public string Name { get; set; } = null!;
        public HashSet<StateFileCoverage> FileCoverage { get; set; } = new();
    }
}