using System.Collections.Generic;

namespace Pruner.Models
{
    internal class State
    {
        public HashSet<StateTest> Tests { get; set; } = new();
    }
}