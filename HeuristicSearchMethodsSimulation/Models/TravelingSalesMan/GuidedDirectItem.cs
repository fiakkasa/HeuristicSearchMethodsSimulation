using System;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record GuidedDirectItem
    {
        public int Index { get; set; }
        public List<LocationGeo> Solution { get; set; } = new();
        public List<List<string>> Report { get; set; } = new();
        public List<GuidedDirectIteration> Iterations { get; } = new();
        public Dictionary<Guid, GuidedDirectIteration> Visited { get; } = new();
        public GuidedDirectIteration? Current => Iterations.Skip(Index).FirstOrDefault();
    }
}
