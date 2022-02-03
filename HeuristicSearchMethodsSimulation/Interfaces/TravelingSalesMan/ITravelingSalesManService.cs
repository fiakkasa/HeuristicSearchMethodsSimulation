using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
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
        TravelingSalesManAlgorithms Algorithm { get; }
        int MinSliderValue { get; }
        int MaxSliderValue { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        bool RouteSymmetry { get; }
        ChartsOptions ChartsOptions { get; }
        MapOptions MapOptions { get; }
        Dictionary<int, List<HistoryIteration>> History { get; set; }

        event Action? OnStateChange;

        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
        Task UpdateState(int sliderValue);
        #endregion

        #region None
        NoneItem? NoneItem { get; set; }
        #endregion

        #region Preselected
        PreselectedItem? PreselectedItem { get; set; }
        #endregion

        #region Exhaustive
        public ExhaustiveItem? ExhaustiveItem { get; set; }

        Task SetExhaustiveItem(ExhaustiveIteration item);
        #endregion

        #region Partial Random
        PartialRandomItem? PartialRandomItem { get; set; }

        Task ClearPartialRandomBuilder();
        Task SetPartialRandomLocation(Guid locationId);
        Task SetPartialRandomLocation(LocationGeo item);

        Task SetPartialRandomItem(PartialRandomIteration item);
        #endregion

        #region Partial Improving
        PartialImprovingItem? PartialImprovingItem { get; set; }

        Task ResetPartialImproving();
        Task PartialImprovingNextIteration();

        #endregion

        #region Guided Direct
        GuidedDirectItem? GuidedDirectItem { get; set; }

        Task SetGuidedDirectSelection(Guid locationId);
        Task SetGuidedDirectSelection(LocationGeo location);
        Task ResetGuidedDirect();
        void SetGuidedDirectRule(int rule);
        #endregion

        #region Evolutionary
        EvolutionaryItem? EvolutionaryItem { get; set; }

        Task SetEvolutionaryLocation(Guid locationId);
        Task SetEvolutionaryLocation(EvolutionaryNode item);
        Task SetEvolutionaryStep(int step);

        Task ResetEvolutionary();
        Task SetEvolutionarySpin();
        #endregion
    }
}