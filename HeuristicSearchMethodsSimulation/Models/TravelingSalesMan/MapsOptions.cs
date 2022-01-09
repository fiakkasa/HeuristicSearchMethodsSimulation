namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record MapsOptions
    {
        internal MapOptions Default { get; set; } = new();
        public MapOptions None { get; set; } = new();
        public MapOptions Preselected { get; set; } = new();
        public MapOptions Exhaustive { get; set; } = new();
        public MapOptions PartialRandom { get; set; } = new();
        public PartialImprovingMapOptions PartialImproving { get; set; } = new();
        public MapOptions GuidedDirect { get; set; } = new();
        public EvolutionaryMapOptions Evolutionary { get; set; } = new();
    }
}
