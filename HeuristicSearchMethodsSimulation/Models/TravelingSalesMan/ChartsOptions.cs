namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record ChartsOptions
    {
        public ChartOptions Default { get; set; } = new();
        public ChartOptions NumberOfUniqueRoutesPerLocations { get; set; } = new();
    }
}
