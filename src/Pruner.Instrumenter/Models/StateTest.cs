using System.Collections.Generic;

namespace Pruner.Models
{
    class StateTest
    {
        public string Name { get; set; }
        public HashSet<StateFileCoverage> FileCoverage { get; set; } = new();
    }
}