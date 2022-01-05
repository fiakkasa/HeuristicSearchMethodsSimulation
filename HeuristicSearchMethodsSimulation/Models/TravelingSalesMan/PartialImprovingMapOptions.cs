namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialImprovingMapOptions : MapOptions
    {
        public virtual string SwapPairALineColor { get; set; } = "red";
        public virtual int SwapPairALineWidth { get; set; } = 2;
        public virtual string SwapPairBLineColor { get; set; } = "green";
        public virtual int SwapPairBLineWidth { get; set; } = 2;
    }
}
