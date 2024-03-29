﻿using Plotly.Blazor;
using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record EvolutionaryItem
    {
        public List<LocationRow> Matrix { get; } = new();
        public string? Text { get; set; }
        public List<LocationRow> ResetMatrix { get; } = new();
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public double? DistanceInKilometers { get; set; }
        public List<EvolutionaryNode> Nodes { get; } = new();
        public Dictionary<Guid, EvolutionaryNode> Visited { get; } = new();
        public bool CycleComplete { get; set; }
        public int NumberOfBitsOffspring { get; set; }
        public int CurrentGenerationIteration { get; set; }
        public int SameResultsCount { get; set; }
        public int Step { get; set; }
        public List<EvolutionaryNodes> CurrentGeneration { get; } = new();
        public List<EvolutionaryNodes> CurrentGenerationPristine { get; } = new();
        public List<EvolutionaryNodes> NextGeneration { get; } = new();
        public List<EvolutionaryNodes> MatingPool { get; } = new();
        public List<EvolutionaryNodes> WheelItems { get; } = new();
        public List<EvolutionaryNodes> Offsprings { get; } = new();
        public bool Spinning { get; set; }
    }
}
