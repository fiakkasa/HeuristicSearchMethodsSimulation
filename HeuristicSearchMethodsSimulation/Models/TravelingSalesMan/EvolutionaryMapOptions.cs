using Plotly.Blazor.Traces.ScatterGeoLib.MarkerLib;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record EvolutionaryMapOptions : MapOptions
    {
        public override SymbolEnum MarkerSymbol { get; set; } = SymbolEnum.CircleOpen;
        public SymbolEnum FirstMarkerSymbol { get; set; } = SymbolEnum.Circle;
        public string FirstMarkerColor { get; set; } = "#5bc0de";
    }
}
