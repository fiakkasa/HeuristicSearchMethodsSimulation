using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record HeadToClosestCityGuidedDirectSolutionItem : GuidedDirectSolutionItem
    {
        public List<LocationGeo> Solution { get; set; } = new();
        public List<GuidedDirectIteration> Iterations { get; } = new();
        public GuidedDirectIteration? Current => Iterations.Skip(Index).FirstOrDefault();
        public GuidedDirectIteration? Next => Iterations.Skip(Index + 1).FirstOrDefault();
    }
}
