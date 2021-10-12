using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public record AppOptions
    {
        public string Title { get; set; } = "HeuristicSearchMethodsSimulation";

        public int Year { get; set; } = DateTime.Now.Year;

        public string Theme { get; set; } = "default";
    }
}
