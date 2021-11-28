namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record MapsOptions
    {
        public MapOptions Default { get; set; } = new();
        public MapOptions None { get; set; } = new();
        public MapOptions Preselected { get; set; } = new();
        public MapOptions Exhaustive { get; set; } = new();
        public MapOptions PartialRandom { get; set; } = new();
        public MapOptions PartialImproving { get; set; } = new();
        public MapOptions GuidedDirect { get; set; } = new();
        public MapOptions Evolutionary { get; set; } = new();
    }
}
