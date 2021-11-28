using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Plotly.Blazor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
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
        List<long> NumberOfUniqueRoutesPerNumberOfLocations { get; }
        long NumberOfUniqueRoutes { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        double? TotalDistanceInKilometers { get; }
        MapOptions MapOptions { get; }
        bool MaxExhaustiveLocationsToCalculateReached { get; }
        List<ExhaustiveItem> ExhaustiveItems { get; }
        ExhaustiveItem? SelectedExhaustiveItem { get; }
        string? PreselectedCycleText { get; }
        bool RouteSymmetry { get; }
        ChartsOptions ChartsOptions { get; }
        List<PartialRandomItem> PartialRandomItems { get; }
        PartialRandomItem? SelectedPartialRandomItem { get; }
        PartialRandomBuilderItem? PartialRandomBuilderItem { get; set; }
        List<ITrace> MapMarkerData { get; }
        PartialImprovingItem? PartialImprovingItem { get; set; }

        event Action? OnStateChange;

        Task ClearPartialRandomBuilder();
        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
        Task ResetPartialImproving();
        Task SetExhaustiveItem(ExhaustiveItem item);
        Task SetPartialRandomItem(PartialRandomItem item);
        Task SetPartialRandomLocation(LocationGeo item);
        Task UpdateState(int sliderValue);
    }
}