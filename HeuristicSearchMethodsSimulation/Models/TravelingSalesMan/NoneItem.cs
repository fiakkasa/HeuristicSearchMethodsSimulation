using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record NoneItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public List<long> NumberOfUniqueRoutesPerNumberOfLocations { get; } = new();
    }
}
