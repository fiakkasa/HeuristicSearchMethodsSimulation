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
        #region Base
        bool IsInit { get; }
        bool Progress { get; }
        List<LocationGeo> Locations { get; }
        bool HasLocations { get; }
        List<LocationGeo> LocationsBySelection { get; }
        List<LocationRow> Matrix { get; }
        List<long> NumberOfUniqueRoutesPerNumberOfLocations { get; }
        long NumberOfUniqueRoutes { get; }
        TravelingSalesManAlgorithms Algorithm { get; }
        int MinSliderValue { get; }
        int MaxSliderValue { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        bool RouteSymmetry { get; }
        double? TotalDistanceInKilometers { get; }
        ChartsOptions ChartsOptions { get; }
        List<ITrace> MapChartData { get; }
        List<ITrace> MapMarkerData { get; }
        List<ITrace> MapLinesData { get; }
        MapOptions MapOptions { get; }

        event Action? OnStateChange;

        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
        Task UpdateState(int sliderValue);
        #endregion

        #region Preselected
        string? PreselectedCycleText { get; }
        #endregion

        #region Exhaustive
        bool MaxExhaustiveLocationsToCalculateReached { get; }
        List<ExhaustiveItem> ExhaustiveItems { get; }
        ExhaustiveItem? SelectedExhaustiveItem { get; }

        Task SetExhaustiveItem(ExhaustiveItem item);
        #endregion

        #region Partial Random
        List<PartialRandomItem> PartialRandomItems { get; }
        PartialRandomItem? SelectedPartialRandomItem { get; }
        PartialRandomBuilderItem? PartialRandomBuilderItem { get; set; }

        Task ClearPartialRandomBuilder();
        Task SetPartialRandomLocation(LocationGeo item);
        Task SetPartialRandomItem(PartialRandomItem item);
        #endregion

        #region Partial Improving
        PartialImprovingItem? PartialImprovingItem { get; set; }

        Task ResetPartialImproving();
        #endregion
    }
}