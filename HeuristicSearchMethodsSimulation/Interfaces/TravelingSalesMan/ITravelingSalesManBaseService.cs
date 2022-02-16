using HeuristicSearchMethodsSimulation.Enums;
using System;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManBaseService
    {
        bool IsInit { get; }
        bool Progress { get; }

        event Action? OnStateChange;

        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
        Task UpdateState(int sliderValue);
    }
}