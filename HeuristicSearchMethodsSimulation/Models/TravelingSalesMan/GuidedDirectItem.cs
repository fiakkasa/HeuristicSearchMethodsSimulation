using Plotly.Blazor;
using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record GuidedDirectItem
    {
        public GuidedDirectSolutionItem HeadToClosestCity { get; set; } = new();
        public GuidedDirectSolutionItem Peripheral { get; set; } = new();
        public long? NumberOfUniqueRoutes { get; set; }
        public List<string> Log { get; set; } = new();
        public int Rule { get; set; } = 1;
        public bool AllowRuleToggle { get; set; }

        public List<LocationRow> Matrix => Rule == 1 ? HeadToClosestCity.Matrix : Peripheral.Matrix;
        public List<LocationRow> ResetMatrix => Rule == 1 ? HeadToClosestCity.ResetMatrix : Peripheral.ResetMatrix;
        public List<ITrace> MapChartData => Rule == 1 ? HeadToClosestCity.MapChartData : Peripheral.MapChartData;
        public List<ITrace> MapMarkerData => Rule == 1 ? HeadToClosestCity.MapMarkerData : Peripheral.MapMarkerData;
        public int Index
        {
            get => Rule == 1 ? HeadToClosestCity.Index : Peripheral.Index;
            set
            {
                if (Rule == 1) HeadToClosestCity.Index = value;
                else Peripheral.Index = value;
            }
        }
        public List<LocationGeo> Solution => Rule == 1 ? HeadToClosestCity.Solution : Peripheral.Solution;
        public List<GuidedDirectIteration> Iterations => Rule == 1 ? HeadToClosestCity.Iterations : Peripheral.Iterations;
        public Dictionary<Guid, GuidedDirectIteration> Visited => Rule == 1 ? HeadToClosestCity.Visited : Peripheral.Visited;
        public GuidedDirectIteration? Current => Rule == 1 ? HeadToClosestCity.Current : Peripheral.Current;
        public GuidedDirectIteration? Next => Rule == 1 ? HeadToClosestCity.Next : Peripheral.Next;
    }
}
