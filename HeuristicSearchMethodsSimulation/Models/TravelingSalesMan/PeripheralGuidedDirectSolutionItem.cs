using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PeripheralGuidedDirectSolutionItem : GuidedDirectSolutionItem
    {
        public List<string> Log { get; set; } = new();
        public List<List<LocationGeo>> Solutions { get; set; } = new();
        public List<LocationGeo> Solution { get; set; } = new();
        public List<GuidedDirectIteration> Iterations { get; } = new();
        public GuidedDirectIteration? Current =>
            Iterations.Count == 0
                ? Visited.Values.FirstOrDefault()
                : Iterations.Skip(Index).FirstOrDefault();
        public GuidedDirectIteration? Next => Iterations.Skip(Index + 1).FirstOrDefault();
    }
}
