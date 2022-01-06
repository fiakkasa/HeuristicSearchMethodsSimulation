using Plotly.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public List<LocationGeo> Solution { get; set; } = new();
        public int Index { get; set; }
        public List<GuidedDirectIteration> Iterations { get; } = new();
        public Dictionary<Guid, GuidedDirectIteration> Visited { get; } = new();
        public GuidedDirectIteration? Current => Iterations.Skip(Index).FirstOrDefault();
        public GuidedDirectIteration? Next => Iterations.Skip(Index + 1).FirstOrDefault();
    }
}
