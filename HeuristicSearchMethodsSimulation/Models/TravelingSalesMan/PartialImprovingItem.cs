using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialImprovingItem
    {
        public string? Text { get; set; }
        public double? DistanceInKilometers { get; set; }
        public List<LocationGeo> ComputedCollection { get; set; } = new();
        public List<LocationPair> ComputedCycle { get; set; } = new();
        public List<LocationGeo> OptimalCollection { get; set; } = new();
        public List<LocationPair> OptimalCycle { get; set; } = new();
        public bool CyclesMatch { get; set; }
        public List<string> Log { get; set; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public List<ITrace> MapLinesData { get; } = new();
    }
}
