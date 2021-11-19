using Plotly.Blazor.Traces.ScatterGeoLib.MarkerLib;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record TravelingSalesManMapOptions
    {
        public string LineColor { get; set; } = "#5bc0de";
        public int LineWidth { get; set; } = 1;
        public string MarkerColor { get; set; } = "#5bc0de";
        public SymbolEnum MarkerSymbol { get; set; } = SymbolEnum.Circle;
        public string LandColor { get; set; } = "rgb(243,243,243)";
        public string CountryColor { get; set; } = "rgb(204,204,204)";
    }
}
