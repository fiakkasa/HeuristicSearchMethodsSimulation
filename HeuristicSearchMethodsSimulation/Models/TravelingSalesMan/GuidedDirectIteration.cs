using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record GuidedDirectIteration(
        int Index,
        string Text,
        double DistanceInKilometers,
        LocationGeo Node,
        List<LocationPair> Cycle,
        List<LocationRow> Matrix,
        List<ITrace> MapChartData,
        List<ITrace> MapMarkerData,
        List<ITrace> MapLinesData
    );
}
