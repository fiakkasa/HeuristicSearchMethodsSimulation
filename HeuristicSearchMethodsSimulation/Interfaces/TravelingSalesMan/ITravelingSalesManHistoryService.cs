using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManHistoryService
    {
        Dictionary<int, List<HistoryIteration>> History { get; }
        void SetHistory(HistoryIteration obj, int sliderValue);
    }
}