using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record EvolutionaryNodes
    {
        public Guid Id { get; set; }
        public List<EvolutionaryNode> Nodes { get; set; } = new();
        public string Text { get; set; } = string.Empty;
        public double? Rank { get; set; }
        public double? DistanceInKilometers { get; set; }
    }
}
