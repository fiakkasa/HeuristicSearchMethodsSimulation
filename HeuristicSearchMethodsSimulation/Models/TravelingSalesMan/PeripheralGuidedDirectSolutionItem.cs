using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PeripheralGuidedDirectSolutionItem : GuidedDirectSolutionItem
    {
        public bool Left { get; set; }
        public List<string> Log { get; set; } = new();
        public List<LocationGeo> Solution { get; set; } = new();
        public List<GuidedDirectIteration> IterationsLeft { get; } = new();
        public List<GuidedDirectIteration> IterationsRight { get; } = new();
        public List<GuidedDirectIteration> Iterations => Left ? IterationsLeft : IterationsRight;
        public GuidedDirectIteration? Current => Iterations.Skip(Index).FirstOrDefault();
        public GuidedDirectIteration? Next => Iterations.Skip(Index + 1).FirstOrDefault();
    }
}
