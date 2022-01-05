using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record PartialImprovingIteration(
        List<LocationGeo> Collection,
        List<LocationPair> Cycle,
        List<LocationRow> Matrix,
        string Log,
        string Text,
        double DistanceInKilometers,
        List<ITrace> MapLinesData
    );
}
