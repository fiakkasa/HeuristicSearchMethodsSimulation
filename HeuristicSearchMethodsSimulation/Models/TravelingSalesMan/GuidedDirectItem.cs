using Plotly.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record GuidedDirectItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public int Index { get; set; }
        public List<LocationGeo> Solution { get; set; } = new();
        public List<GuidedDirectIteration> Iterations { get; } = new();
        public Dictionary<Guid, GuidedDirectIteration> Visited { get; } = new();
        public GuidedDirectIteration? Current => Iterations.Skip(Index).FirstOrDefault();
    }
}
