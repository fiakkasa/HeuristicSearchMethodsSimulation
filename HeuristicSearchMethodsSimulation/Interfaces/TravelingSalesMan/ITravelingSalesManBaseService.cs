using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManBaseService
    {
        bool IsInit { get; }
        bool Progress { get; }
        List<LocationGeo> Locations { get; }
        bool HasLocations { get; }
        List<LocationGeo> LocationsBySelection { get; }
        TravelingSalesManAlgorithms Algorithm { get; }
        int MinSliderValue { get; }
        int MaxSliderValue { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        bool RouteSymmetry { get; }
        ChartsOptions ChartsOptions { get; }
        MapOptions MapOptions { get; }

        event Action? OnStateChange;

        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
        Task UpdateState(int sliderValue);
    }
}