using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManHistoryService
    {
        IReadOnlyDictionary<int, List<HistoryIteration>> History { get; }
        Task SetHistory(HistoryIteration obj, int sliderValue, CancellationToken cancellationToken);
    }
}