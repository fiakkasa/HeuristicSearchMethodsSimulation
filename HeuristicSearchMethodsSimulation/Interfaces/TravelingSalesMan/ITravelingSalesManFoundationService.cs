using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManFoundationService
    {
        TravelingSalesManAlgorithms Algorithm { get; }
        ChartsOptions ChartsOptions { get; }
        IMongoClient Client { get; }
        bool HasLocations { get; }
        bool IsInit { get; }
        List<LocationGeo> Locations { get; }
        List<LocationGeo> LocationsBySelection { get; }
        MapsOptions MapsOptions { get; }
        MapOptions MapOptions { get; }
        int MaxExhaustiveLocationsToCalculate { get; }
        int MaxSliderValue { get; }
        int MinSliderValue { get; }
        bool Progress { get; }
        bool RouteSymmetry { get; }
        int SliderStepValue { get; }
        int SliderValue { get; }
        string DatabaseName { get; }
        int FetchLimit { get; }
        int InitialSliderValue { get; }

        Task Delay(int time = 250, CancellationToken cancellationToken = default);
        Task Init(TravelingSalesManAlgorithms algo, CancellationToken cancellationToken = default);
        Task Refresh(CancellationToken cancellationToken = default);
        void UpdateState(int sliderValue, TravelingSalesManAlgorithms algo);
    }
}