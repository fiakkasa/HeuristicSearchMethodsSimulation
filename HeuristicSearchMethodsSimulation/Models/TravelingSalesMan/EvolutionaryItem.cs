﻿using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record EvolutionaryItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public double? DistanceInKilometers { get; set; }
    }
}
