using Plotly.Blazor.Traces.ScatterGeoLib.MarkerLib;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record MapOptions : ChartOptions<SymbolEnum>
    {
        public override SymbolEnum MarkerSymbol { get; set; } = SymbolEnum.Circle;
        public string LandColor { get; set; } = "rgb(243,243,243)";
        public string CountryColor { get; set; } = "rgb(204,204,204)";
    }
}
