using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record ExhaustiveItem
    {
        public List<LocationGeo> Collection { get; set; } = new();

        public string Text { get; set; } = string.Empty;
    }
}
