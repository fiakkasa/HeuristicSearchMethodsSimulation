using Plotly.Blazor.Traces.ScatterLib.MarkerLib;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record ChartOptions<TMarkerSymbol> where TMarkerSymbol : struct
    {
        public virtual string LineColor { get; set; } = "#5bc0de";
        public virtual int LineWidth { get; set; } = 1;
        public virtual string MarkerColor { get; set; } = "#5bc0de";
        public virtual TMarkerSymbol MarkerSymbol { get; set; }
    }

    public record ChartOptions : ChartOptions<SymbolEnum>
    {
        public override SymbolEnum MarkerSymbol { get; set; } = SymbolEnum.Circle;
    }
}
