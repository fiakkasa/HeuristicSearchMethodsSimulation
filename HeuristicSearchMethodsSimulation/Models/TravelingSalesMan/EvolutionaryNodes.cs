using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record EvolutionaryNodes
    {
        public List<EvolutionaryNode> Nodes { get; set; } = new();
        public double? Rank { get; set; }
        public double? DistanceInKilometers { get; set; }
    }
}
