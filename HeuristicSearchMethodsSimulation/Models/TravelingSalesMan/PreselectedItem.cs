using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PreselectedItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public string? Text { get; set; }
        public double? DistanceInKilometers { get; set; }
    }
}
