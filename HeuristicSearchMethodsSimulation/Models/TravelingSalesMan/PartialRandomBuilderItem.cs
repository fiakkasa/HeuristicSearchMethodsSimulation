using Plotly.Blazor;
using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialRandomBuilderItem
    {
        public Dictionary<Guid, LocationGeo> Collection { get; } = new();
        public string? Text { get; set; }
        public double? DistanceInKilometers { get; set; }
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
    }
}
