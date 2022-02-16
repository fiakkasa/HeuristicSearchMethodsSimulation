using AutoMapper;
using GeoCoordinatePortable;
using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Extensions;
using HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Location = HeuristicSearchMethodsSimulation.Models.TravelingSalesMan.Location;

namespace HeuristicSearchMethodsSimulation.Services.TravelingSalesMan
{
    public class TravelingSalesManFoundationService : IDisposable, ITravelingSalesManFoundationService
    {
        private bool _disposedValue;
        private readonly IOptions<MongoOptions> _mongoOptions;
        private readonly IOptions<TravelingSalesManOptions> _travelingSalesManOptions;
        private readonly Func<IMongoClient> _mongoClientFactory;
        private readonly IMapper _mapper;
        private readonly ITravelingSalesManHistoryService _travelingSalesManHistoryService;
        private readonly ILogger<TravelingSalesManService> _logger;
        private readonly IMongoClient? _client;
        private readonly CancellationTokenSource _cts = new();
        private bool _isInitializing;
        private int _initialSliderValue = 1;
        private int _fetchLimit = 100;
        private int _maxExhaustiveLocationsToCalculate = 7;

        public IMongoClient Client => _client ?? _mongoClientFactory();
        public string DatabaseName => MongoOptions.Databases.Data;
        private MongoOptions MongoOptions => _mongoOptions.Value;
        private TravelingSalesManOptions TravelingSalesManOptions => _travelingSalesManOptions.Value;
        private IMongoCollection<Location> LocationsCollection => Client.GetCollection<Location>(MongoOptions.Databases.Data);

        public bool IsInit { get; private set; }
        public bool Progress { get; private set; }
        public List<LocationGeo> Locations { get; } = new();
        public bool HasLocations => !Locations.HasInsufficientData();
        public List<LocationGeo> LocationsBySelection { get; } = new();
        public TravelingSalesManAlgorithms Algorithm { get; private set; }
        public int MinSliderValue { get; private set; }
        public int MaxSliderValue { get; private set; }
        public int SliderStepValue { get; private set; }
        public int SliderValue { get; private set; }
        public bool RouteSymmetry { get; } = true;
        public ChartsOptions ChartsOptions { get; private set; } = new();
        public MapsOptions MapsOptions { get; private set; } = new();
        public MapOptions MapOptions => Algorithm switch
        {
            TravelingSalesManAlgorithms.Evolutionary => MapsOptions.Evolutionary,
            TravelingSalesManAlgorithms.Exhaustive => MapsOptions.Exhaustive,
            TravelingSalesManAlgorithms.Guided_Direct => MapsOptions.GuidedDirect,
            TravelingSalesManAlgorithms.None => MapsOptions.None,
            TravelingSalesManAlgorithms.Preselected => MapsOptions.Preselected,
            TravelingSalesManAlgorithms.Partial_Improving => MapsOptions.PartialImproving,
            TravelingSalesManAlgorithms.Partial_Random => MapsOptions.PartialRandom,
            _ => MapsOptions.Default
        };
        public int MaxExhaustiveLocationsToCalculate => _maxExhaustiveLocationsToCalculate;
        public int FetchLimit => _fetchLimit;
        public int InitialSliderValue => _initialSliderValue;

        public TravelingSalesManFoundationService(
           IOptions<MongoOptions> mongoOptions,
           IOptions<TravelingSalesManOptions> travelingSalesManOptions,
           Func<IMongoClient> mongoClientFactory,
           IMapper mapper,
           ITravelingSalesManHistoryService travelingSalesManHistoryService,
           ILogger<TravelingSalesManService> logger
       )
        {
            _mongoOptions = mongoOptions;
            _travelingSalesManOptions = travelingSalesManOptions;
            _mongoClientFactory = mongoClientFactory;
            _mapper = mapper;
            _travelingSalesManHistoryService = travelingSalesManHistoryService;
            _logger = logger;

            InitValuesFromOptions();
        }

        private async Task SetDataFromDatabase(CancellationToken cancellationToken)
        {
            var locations = await Fetch(_fetchLimit, cancellationToken).ConfigureAwait(true);
            Locations.Clear();
            Locations.AddRange(locations);
        }

        public async Task Refresh(CancellationToken cancellationToken = default)
        {
            Progress = true;

            await SetDataFromDatabase(cancellationToken).ConfigureAwait(true);

            Progress = false;
        }

        public async Task Init(TravelingSalesManAlgorithms algo, CancellationToken cancellationToken = default)
        {
            Algorithm = algo;

            if (_isInitializing) return;

            _isInitializing = true;

            Progress = true;

            if (!HasLocations)
                await SetDataFromDatabase(cancellationToken).ConfigureAwait(true);

            IsInit = true;
            Progress = false;
            _isInitializing = false;
        }

        public async Task Delay(int time = 250, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(time, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void InitValuesFromOptions()
        {
            try
            {
                MinSliderValue = TravelingSalesManOptions.MinSliderValue;
                MaxSliderValue = TravelingSalesManOptions.MaxSliderValue;
                SliderStepValue = TravelingSalesManOptions.SliderStepValue;
                SliderValue = TravelingSalesManOptions.InitialSliderValue;
                _initialSliderValue = TravelingSalesManOptions.InitialSliderValue;
                _fetchLimit = TravelingSalesManOptions.FetchLimit;
                ChartsOptions = TravelingSalesManOptions.Charts;
                MapsOptions = TravelingSalesManOptions.Maps;
                _maxExhaustiveLocationsToCalculate = TravelingSalesManOptions.MaxExhaustiveLocationsToCalculate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<List<LocationGeo>> Fetch(int limit, CancellationToken cancellationToken)
        {
            Progress = true;

            try
            {
                return
                (
                    await LocationsCollection
                        .Find(Builders<Location>.Filter.Empty)
                        .SortBy(x => x.Ordinal)
                        .ThenBy(x => x.Label)
                        .ThenBy(x => x.ShortCode)
                        .ThenBy(x => x.Id)
                        .Limit(limit)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true)
                )
                .ConvertAll(x =>
                    _mapper.Map<LocationGeo>(x) with
                    {
                        Geo = new GeoCoordinate(x.Latitude, x.Longitude)
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new();
            }
        }

        public void UpdateState(int sliderValue, TravelingSalesManAlgorithms algo)
        {
            SliderValue = sliderValue;
            Algorithm = algo;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;

            _cts.Cancel();

            _disposedValue = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
