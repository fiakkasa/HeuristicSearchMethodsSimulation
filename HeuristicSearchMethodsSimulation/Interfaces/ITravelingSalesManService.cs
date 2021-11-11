using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces
{
    public interface ITravelingSalesManService
    {
        TravelingSalesManAlgorithms Algorithm { get; }
        bool HasLocations { get; }
        bool IsInit { get; }
        bool Loading { get; }
        List<LocationGeo> Locations { get; }
        List<LocationGeo> LocationsBySelection { get; }
        List<ITrace> MapChartData { get; }
        List<LocationRow> Matrix { get; }
        int MaxSliderValue { get; }
        int MinSliderValue { get; }
        List<long> NumberOfUniqueLocations { get; }
        long NumberOfUniqueRoutes { get; }
        Pie? PieChartData { get; }
        bool RouteSymmetry { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        long? TotalDistance { get; }

        event Action? OnStateChange;

        Task Init(TravelingSalesManAlgorithms algo = TravelingSalesManAlgorithms.None);
        Task Refresh();
        Task SetRouteSymmetry(bool routeSymmetry);
        Task UpdateState(int sliderValue);
    }
}