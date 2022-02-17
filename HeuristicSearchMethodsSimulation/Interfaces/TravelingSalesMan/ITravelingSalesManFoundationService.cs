using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
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
        IReadOnlyList<LocationGeo> Locations { get; }
        IReadOnlyList<LocationGeo> LocationsBySelection { get; }
        MapsOptions MapsOptions { get; }
        MapOptions MapOptions { get; }
        int MaxExhaustiveLocationsToCalculate { get; }
        int MaxSliderValue { get; }
        int MinSliderValue { get; }
        bool Progress { get; }
        bool RouteSymmetry { get; }
        int SliderStepValue { get; }
        int SliderValue { get; set; }
        string DatabaseName { get; }

        event Action? OnInitComplete;
        event Action? OnProgress;

        Task Init(TravelingSalesManAlgorithms algo);
        Task Refresh();
    }
}