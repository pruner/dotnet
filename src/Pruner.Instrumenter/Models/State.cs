using System.Collections.Generic;

namespace Pruner.Instrumenter.Models
{
    internal class State
    {
        public HashSet<StateTest> Tests { get; set; } = new();
    }
}