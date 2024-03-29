﻿using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialRandomItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public List<PartialRandomIteration> Iterations { get; } = new();
        public PartialRandomIteration? SelectedIteration { get; set; }
        public PartialRandomBuilderItem? Builder { get; set; }
    }
}
