using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialImprovingItem
    {
        public string? Text { get; set; }
        public double? DistanceInKilometers { get; set; }
        public List<LocationPair> ComputedCycle { get; set; } = new();
        public List<LocationPair> OptimalCycle { get; set; } = new();
        public List<string> Log { get; set; } = new();
    }
}
