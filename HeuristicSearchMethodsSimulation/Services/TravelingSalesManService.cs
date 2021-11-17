using AutoMapper;
using GeoCoordinatePortable;
using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Extensions;
using HeuristicSearchMethodsSimulation.Interfaces;
using HeuristicSearchMethodsSimulation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoverInfoFlag = Plotly.Blazor.Traces.ScatterGeoLib.HoverInfoFlag;
using Location = HeuristicSearchMethodsSimulation.Models.Location;

namespace HeuristicSearchMethodsSimulation.Services
{
    public class TravelingSalesManService : IDisposable, ITravelingSalesManService
    {
        private bool _disposedValue;
        private readonly IOptions<MongoOptions> _mongoOptions;
        private readonly IOptions<TravelingSalesManOptions> _travelingSalesManOptions;
        private readonly Func<IMongoClient> _mongoClientFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<TravelingSalesManService> _logger;
        private readonly IMongoClient? _client;
        private readonly CancellationTokenSource _cts = new();
        private bool _isInitializing;
        private int _initialSliderValue = 1;
        private int _fetchLimit = 100;
        private TravelingSalesManMapsOptions _mapOptions = new();

        private event Action? OnStateChangeDelegate;

        public event Action? OnStateChange
        {
            add
            {
                OnStateChangeDelegate += value;
            }
            remove
            {
                OnStateChangeDelegate -= value;
            }
        }

        private IMongoClient Client => _client ?? _mongoClientFactory();
        private MongoOptions MongoOptions => _mongoOptions.Value;
        private TravelingSalesManOptions TravelingSalesManOptions => _travelingSalesManOptions.Value;
        private IMongoCollection<Location> LocationsCollection => Client.GetCollection<Location>(MongoOptions.Databases.Data);

        public bool IsInit { get; private set; }
        public bool Loading { get; private set; }
        public List<LocationGeo> Locations { get; } = new();
        public bool HasLocations => Locations.Count > 0;
        public List<LocationGeo> LocationsBySelection { get; } = new();
        public List<LocationRow> Matrix { get; } = new();
        public List<long> NumberOfUniqueRoutesPerNumberOfLocations { get; } = new();
        public long NumberOfUniqueRoutes => NumberOfUniqueRoutesPerNumberOfLocations.LastOrDefault();
        public bool RouteSymmetry { get; private set; }
        public TravelingSalesManAlgorithms Algorithm { get; private set; }
        public int MinSliderValue { get; private set; }
        public int MaxSliderValue { get; private set; }
        public int SliderStepValue { get; private set; }
        public int SliderValue { get; private set; }
        public double? TotalDistanceInKilometers { get; private set; }
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public List<ITrace> MapLinesData { get; } = new();
        public TravelingSalesManMapOptions MapOptions { get; private set; } = new();
        public Pie? PieChartData { get; }

        public TravelingSalesManService(
            IOptions<MongoOptions> mongoOptions,
            IOptions<TravelingSalesManOptions> travelingSalesManOptions,
            Func<IMongoClient> mongoClientFactory,
            IMapper mapper,
            ILogger<TravelingSalesManService> logger
        )
        {
            _mongoOptions = mongoOptions;
            _travelingSalesManOptions = travelingSalesManOptions;
            _mongoClientFactory = mongoClientFactory;
            _mapper = mapper;
            _logger = logger;

            InitValuesFromOptions();
        }

        public async Task SetRouteSymmetry(bool routeSymmetry)
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            await Delay()
                .ContinueWith(_ =>
                {
                    RouteSymmetry = routeSymmetry;
                    Loading = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        public async Task UpdateState(int sliderValue)
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(
                new[]
                {
                    UpdateState(sliderValue, Algorithm, _cts.Token),
                    Delay()
                }
            )
            .ContinueWith(_ =>
            {
                Loading = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(true);
        }

        public async Task Refresh()
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            var locations = await Fetch(_fetchLimit, _cts.Token).ConfigureAwait(true);

            Locations.Clear();
            Locations.AddRange(locations);

            await Task.WhenAll(
                new[]
                {
                    UpdateState(HasLocations ? SliderValue : _initialSliderValue, Algorithm, _cts.Token),
                    Delay()
                }
            )
            .ContinueWith(_ =>
            {
                Loading = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(true);
        }

        public async Task Init(TravelingSalesManAlgorithms algo)
        {
            SetPivotValues(SliderValue, algo);

            if (_isInitializing) return;

            _isInitializing = true;

            Loading = true;

            OnStateChangeDelegate?.Invoke();

            if (!HasLocations)
            {
                var locations = await Fetch(_fetchLimit, _cts.Token).ConfigureAwait(true);

                Locations.Clear();
                Locations.AddRange(locations);
            }

            await UpdateState(HasLocations ? SliderValue : _initialSliderValue, Algorithm, _cts.Token)
                .ContinueWith(_ =>
                {
                    IsInit = true;
                    Loading = false;
                    _isInitializing = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        private async Task Delay()
        {
            try
            {
                await Task.Delay(250, _cts.Token).ConfigureAwait(true);
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
                _mapOptions = TravelingSalesManOptions.Map;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<List<LocationGeo>> Fetch(int limit, CancellationToken cancellationToken)
        {
            Loading = true;

            try
            {
                return (
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

                return new List<LocationGeo>();
            }
        }

        private TravelingSalesManMapOptions ResolveMapOptions(TravelingSalesManAlgorithms algo) => algo switch
        {
            TravelingSalesManAlgorithms.Evolutionary => _mapOptions.Evolutionary,
            TravelingSalesManAlgorithms.Exhaustive => _mapOptions.Exhaustive,
            TravelingSalesManAlgorithms.Guided_Direct => _mapOptions.GuidedDirect,
            TravelingSalesManAlgorithms.None => _mapOptions.None,
            TravelingSalesManAlgorithms.Ordinal_Based_Cycle => _mapOptions.OrdinalBasedCycle,
            TravelingSalesManAlgorithms.Partial_Improving => _mapOptions.PartialImproving,
            TravelingSalesManAlgorithms.Partial_Random => _mapOptions.PartialRandom,
            _ => _mapOptions.Default
        };

        private void SetPivotValues(int sliderValue, TravelingSalesManAlgorithms algo)
        {
            SliderValue = sliderValue;
            Algorithm = algo;
            MapOptions = ResolveMapOptions(algo);
        }

        private async Task UpdateState(int sliderValue, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            SetPivotValues(sliderValue, algo);

            try
            {
                var locationsBySelection = Locations.Take(sliderValue).ToList();
                var matrix = await CalculateMatrix(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutes = await CalculateNumberOfUniqueRoutesPerNumberOfLocations(sliderValue, cancellationToken).ConfigureAwait(true);
                var mapMarkerData = await CalculateMapMarkers(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);
                var mapLineData = await CalculateMapLines(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);
                var totalDistance = await CalculateTotalDistance(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);

                LocationsBySelection.Clear();
                Matrix.Clear();
                NumberOfUniqueRoutesPerNumberOfLocations.Clear();
                MapChartData.Clear();
                MapMarkerData.Clear();
                MapLinesData.Clear();

                LocationsBySelection.AddRange(locationsBySelection);
                Matrix.AddRange(matrix);
                NumberOfUniqueRoutesPerNumberOfLocations.AddRange(numberOfUniqueRoutes);
                MapChartData.AddRange(mapLineData.Concat(mapMarkerData));
                MapMarkerData.AddRange(mapMarkerData);
                MapLinesData.AddRange(mapLineData);
                TotalDistanceInKilometers = totalDistance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private static double CalculateDistancePointToPointInKilometers(LocationGeo location, LocationGeo otherLocation) =>
            (location, otherLocation) is { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } }
                ? lcGeo.GetDistanceTo(olcGeo) / 1000
                : 0D;

        private async Task<double?> CalculateTotalDistance(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.Count < 2) return 0D;

                return await Task.Run(() =>
                    algo switch
                    {
                        TravelingSalesManAlgorithms.None => default(double?),
                        TravelingSalesManAlgorithms.Ordinal_Based_Cycle =>
                            locations
                                .Append(locations[0])
                                .Skip(1)
                                .Select((location, i) => CalculateDistancePointToPointInKilometers(location, locations[i]))
                                .Sum()
                        ,
                        _ => 0D
                    },
                    cancellationToken
                ).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return 0D;
            }
        }

        private async Task<List<ITrace>> CalculateMapLines(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.Count < 2) return new List<ITrace>();

                return await Task.Run(() =>
                    algo switch
                    {
                        TravelingSalesManAlgorithms.Ordinal_Based_Cycle =>
                            locations
                                .Skip(1)
                                .Append(locations[0])
                                .Select((x, i) =>
                                    new ScatterGeo
                                    {
                                        LocationMode = LocationModeEnum.ISO3,
                                        Lon = new List<object> { locations[i].Longitude, x.Longitude },
                                        Lat = new List<object> { locations[i].Latitude, x.Latitude },
                                        Mode = ModeFlag.Lines
                                    }
                                )
                                .ToList<ITrace>(),
                        _ => new List<ITrace>()
                    },
                    cancellationToken
                ).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<ITrace>();
            }
        }

        private async Task<List<ITrace>> CalculateMapMarkers(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.Count == 0)
                    return new List<ITrace> { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };

                return await Task.Run(
                    () => algo switch
                    {
                        TravelingSalesManAlgorithms.Evolutionary =>
                            locations.Select((x, i) =>
                                 new ScatterGeo
                                 {
                                     LocationMode = LocationModeEnum.ISO3,
                                     Lon = new List<object> { x.Longitude },
                                     Lat = new List<object> { x.Latitude },
                                     Mode = ModeFlag.Markers,
                                     Text = $"{x.Label} ({x.ShortCode})",
                                     Name = $"{x.Label} ({x.ShortCode})",
                                     HoverLabel = new() { NameLength = 0 },
                                     HoverTemplate = $"Ordinal: {i + 1}<br />%{{fullData.text}}<br />{nameof(HoverInfoFlag.Lat)}: %{{lat}}<br />{nameof(HoverInfoFlag.Lon)}: %{{lon}}"
                                 }
                            ).ToList<ITrace>(),
                        TravelingSalesManAlgorithms.Ordinal_Based_Cycle =>
                            locations.Select((x, i) =>
                                new ScatterGeo
                                {
                                    LocationMode = LocationModeEnum.ISO3,
                                    Lon = new List<object> { x.Longitude },
                                    Lat = new List<object> { x.Latitude },
                                    Mode = ModeFlag.Markers,
                                    Text = $"{x.Label} ({x.ShortCode})",
                                    Name = $"{x.Label} ({x.ShortCode})",
                                    HoverLabel = new() { NameLength = 0 },
                                    HoverTemplate = $"Ordinal: {x.Ordinal}<br />%{{fullData.text}}<br />{nameof(HoverInfoFlag.Lat)}: %{{lat}}<br />{nameof(HoverInfoFlag.Lon)}: %{{lon}}"
                                }
                            ).ToList<ITrace>(),
                        _ => locations.ConvertAll<ITrace>(x =>
                             new ScatterGeo
                             {
                                 LocationMode = LocationModeEnum.ISO3,
                                 Lon = new List<object> { x.Longitude },
                                 Lat = new List<object> { x.Latitude },
                                 Mode = ModeFlag.Markers,
                                 Text = $"{x.Label} ({x.ShortCode})",
                                 Name = $"{x.Label} ({x.ShortCode})",
                                 HoverLabel = new() { NameLength = 0 },
                                 HoverTemplate = $"%{{fullData.text}}<br />{nameof(HoverInfoFlag.Lat)}: %{{lat}}<br />{nameof(HoverInfoFlag.Lon)}: %{{lon}}"
                             }
                        )
                    },
                    cancellationToken
                ).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<ITrace> { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };
            }
        }

        private async Task<List<long>> CalculateNumberOfUniqueRoutesPerNumberOfLocations(int numberOfLocations, CancellationToken cancellationToken)
        {
            try
            {
                if (numberOfLocations < 1) return new List<long>();

                return await Task.Run(() =>
                    Enumerable.Range(1, numberOfLocations)
                        .Select(i => Enumerable.Range(1, i).Aggregate(1L, (f, x) => f * x))
                        .ToList(),
                    cancellationToken
                ).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<long>();
            }
        }

        private List<LocationRow> MarkIsHighlightedDistance(List<LocationRow> matrix, TravelingSalesManAlgorithms algo)
        {
            if (algo == TravelingSalesManAlgorithms.Ordinal_Based_Cycle)
            {
                return matrix
                    .Select((row, rowIndex) =>
                        row with
                        {
                            Collection = row.Collection
                                .Select((cell, cellIndex) =>
                                    rowIndex + 1 == cellIndex ||
                                    (rowIndex + 1 == row.Collection.Count && cellIndex == 0)
                                        ? cell with { IsHighlightedDistance = true }
                                        : cell
                                )
                                .ToList()
                        }
                    )
                    .ToList();
            }

            return matrix;
        }

        private async Task<List<LocationRow>> CalculateMatrix(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() =>
                    {
                        var matrix =
                            locations.ConvertAll(
                            location =>
                            {
                                var rowCollection =
                                    locations.Select(
                                        (otherLocation, index) =>
                                            new LocationToLocation(
                                                A: location,
                                                B: otherLocation,
                                                DirectionalKey: $"{location.ShortCode}-{otherLocation.ShortCode}",
                                                ReverseDirectionalKey: $"{otherLocation.ShortCode}-{location.ShortCode}",
                                                Key: location.ShortCode.CompareTo(otherLocation.ShortCode) <= 0
                                                    ? $"{location.ShortCode}-{otherLocation.ShortCode}"
                                                    : $"{otherLocation.ShortCode}-{location.ShortCode}",
                                                DistanceInKilometers: CalculateDistancePointToPointInKilometers(location, otherLocation),
                                                Index: index,
                                                OrdinalFromOrigin: 0,
                                                IsHighlightedDistance: false
                                            )
                                        )
                                        .OrderBy(x => x.DistanceInKilometers)
                                        .Select((x, ordinalFromOrigin) =>
                                            x with
                                            {
                                                OrdinalFromOrigin = ordinalFromOrigin
                                            }
                                        )
                                        .OrderBy(x => x.Index)
                                        .ToList();

                                var selfKey = $"{location.ShortCode}-{location.ShortCode}";

                                return new LocationRow(
                                    Collection: rowCollection,
                                    Ylabel: $"{location.Label} ({location.ShortCode})",
                                    Xlabels: locations.ConvertAll(x => $"{x.Label} ({x.ShortCode})"),
                                    Min: rowCollection.Where(x => x.Key != selfKey).Min(),
                                    Max: rowCollection.Where(x => x.Key != selfKey).Max()
                                );
                            }
                        );

                        return MarkIsHighlightedDistance(matrix, algo);
                    },
                    cancellationToken
                )
                .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<LocationRow>();
            }
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
