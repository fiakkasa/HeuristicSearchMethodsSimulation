using Plotly.Blazor;
using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record GuidedDirectSolutionItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public string? Text { get; set; }
        public bool Completed { get; set; }
        public int Index { get; set; }
        public Dictionary<Guid, GuidedDirectIteration> Visited { get; } = new();
    }
}
