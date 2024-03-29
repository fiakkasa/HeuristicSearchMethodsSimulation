﻿using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialImprovingItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public string? Text { get; set; }
        public double? DistanceInKilometers { get; set; }
        public List<LocationGeo> ComputedCollection { get; } = new();
        public List<LocationPair> ComputedCycle { get; } = new();
        public int Iteration { get; set; }
        public List<PartialImprovingIteration> Iterations { get; } = new();
        public bool CyclesMatch { get; set; }
        public List<string> Log { get; set; } = new();
    }
}
