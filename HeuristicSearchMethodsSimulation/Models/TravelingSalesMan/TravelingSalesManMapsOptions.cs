namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record TravelingSalesManMapsOptions
    {
        public TravelingSalesManMapOptions Default { get; set; } = new();
        public TravelingSalesManMapOptions None { get; set; } = new();
        public TravelingSalesManMapOptions Preselected { get; set; } = new();
        public TravelingSalesManMapOptions Exhaustive { get; set; } = new();
        public TravelingSalesManMapOptions PartialRandom { get; set; } = new();
        public TravelingSalesManMapOptions PartialImproving { get; set; } = new();
        public TravelingSalesManMapOptions GuidedDirect { get; set; } = new();
        public TravelingSalesManMapOptions Evolutionary { get; set; } = new();
    }
}
