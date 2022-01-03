using HeuristicSearchMethodsSimulation.Enums;
using Plotly.Blazor;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record HistoryIteration(TravelingSalesManAlgorithms Algo, string Text, double DistanceInKilometers, List<ITrace> MapLinesData);
}
