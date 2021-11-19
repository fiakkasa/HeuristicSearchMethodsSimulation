namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record TravelingSalesManMapsOptions
    {
        public TravelingSalesManMapOptions None { get; set; } = new();
        public TravelingSalesManMapOptions PartialRandom { get; set; } = new();
        public TravelingSalesManMapOptions Exhaustive { get; set; } = new();
        public TravelingSalesManMapOptions PartialImproving { get; set; } = new();
        public TravelingSalesManMapOptions GuidedDirect { get; set; } = new();
        public TravelingSalesManMapOptions Evolutionary { get; set; } = new();
        public TravelingSalesManMapOptions OrdinalBasedCycle { get; set; } = new();
        public TravelingSalesManMapOptions Default { get; set; } = new();
    }
}
