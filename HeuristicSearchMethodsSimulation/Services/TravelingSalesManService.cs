﻿using AutoMapper;
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
        private int _fetchLimit = 100;
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
        public List<long> NumberOfUniqueLocations { get; } = new();
        public long NumberOfUniqueRoutes => NumberOfUniqueLocations.LastOrDefault();
        public bool RouteSymmetry { get; private set; }
        public TravelingSalesManAlgorithms Algorithm { get; private set; }
        public int MinSliderValue { get; private set; }
        public int MaxSliderValue { get; private set; }
        public int SliderStepValue { get; private set; }
        public int SliderValue { get; private set; }

        public List<ITrace> MapChartData { get; } = new();
        public Pie? PieChartData { get; private set; }

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
                .ConfigureAwait(false);
        }

        public async Task SetAlgo(TravelingSalesManAlgorithms algo)
        {
            Algorithm = algo;

            if (Algorithm == TravelingSalesManAlgorithms.None)
            {
                Reset();
                OnStateChangeDelegate?.Invoke();

                return;
            }

            Loading = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(
                new[]
                {
                    UpdateState(TravelingSalesManOptions.InitialSliderValue, _cts.Token),
                    Delay()
                }
            )
            .ContinueWith(_ =>
            {
                Loading = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(false);
        }

        public async Task UpdateState(int sliderValue)
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(
                new[]
                {
                    UpdateState(sliderValue, _cts.Token),
                    Delay()
                }
            )
            .ContinueWith(_ =>
            {
                Loading = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(false);
        }

        public async Task Refresh()
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            var locations = await Fetch(_fetchLimit, _cts.Token).ConfigureAwait(false);

            Locations.Clear();
            Locations.AddRange(locations);

            await Task.WhenAll(
                new[]
                {
                    HasLocations
                        ? UpdateState(SliderValue, _cts.Token)
                        : Task.Run(() => Reset()),
                    Delay()
                }
            )
            .ContinueWith(_ =>
            {
                Loading = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(false);
        }

        public async Task Init(TravelingSalesManAlgorithms algo = TravelingSalesManAlgorithms.None)
        {
            if (_isInitializing) return;

            _isInitializing = true;

            IsInit = false;
            Loading = true;

            OnStateChangeDelegate?.Invoke();

            var locations = await Fetch(_fetchLimit, _cts.Token).ConfigureAwait(false);

            Locations.Clear();
            Locations.AddRange(locations);

            Algorithm = algo;

            await (
                HasLocations
                    ? UpdateState(SliderValue, _cts.Token)
                    : Task.Run(() => Reset())
            )
            .ContinueWith(_ =>
            {
                IsInit = true;
                Loading = false;
                _isInitializing = false;
                OnStateChangeDelegate?.Invoke();
            })
            .ConfigureAwait(false);
        }

        private async Task Delay()
        {
            try
            {
                await Task.Delay(250, _cts.Token).ConfigureAwait(false);
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
                _fetchLimit = TravelingSalesManOptions.FetchLimit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void Reset()
        {
            try
            {
                MapChartData.Clear();
                PieChartData = default;

                LocationsBySelection.Clear();
                Matrix.Clear();
                NumberOfUniqueLocations.Clear();

                SliderValue = TravelingSalesManOptions.InitialSliderValue;
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
                        .ConfigureAwait(false)
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

        private async Task UpdateState(int sliderValue, CancellationToken cancellationToken)
        {
            try
            {
                SliderValue = sliderValue;

                var locationsBySelection = Locations.Take(sliderValue).ToList();
                var matrix = await CalculateMatrix(locationsBySelection, cancellationToken).ConfigureAwait(false);
                var numberOfUniqueLocations = await CalculateNumberOfUniqueRoutes(sliderValue, cancellationToken).ConfigureAwait(false);
                var mapMarkerData =
                  await CalculateMapMarkers(locationsBySelection, cancellationToken).ConfigureAwait(false) is { Count: > 0 } mc
                      ? mc
                      : new List<ITrace> { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };
                var samplePieData =
                    await Task.Run(
                        () =>
                        {
                            var pieChartDataDict =
                                matrix.Aggregate(
                                    new Dictionary<string, LocationToLocation>(),
                                    (result, row) =>
                                    {
                                        if (row.Min is { } min)
                                            result[min.Key] = min;

                                        return result;
                                    }
                                );

                            return new Pie
                            {
                                Labels = pieChartDataDict.Values.Select(x => $"{x.DirectionalKey} | {x.ReverseDirectionalKey}").ToList<object>(),
                                Values = pieChartDataDict.Values.Select(x => (object)(long)(x.DistanceInKilometers / 1000)).ToList()
                            };
                        },
                        cancellationToken
                    ).ConfigureAwait(false);

                LocationsBySelection.Clear();
                Matrix.Clear();
                NumberOfUniqueLocations.Clear();
                MapChartData.Clear();
                PieChartData = default;

                LocationsBySelection.AddRange(locationsBySelection);
                Matrix.AddRange(matrix);
                NumberOfUniqueLocations.AddRange(numberOfUniqueLocations);
                MapChartData.AddRange(mapMarkerData);
                PieChartData = samplePieData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<List<ITrace>> CalculateMapMarkers(List<LocationGeo> locations, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() =>
                    locations.Count == 0
                        ? new List<ITrace> { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } }
                        : locations.ConvertAll<ITrace>(x =>
                            new ScatterGeo
                            {
                                LocationMode = LocationModeEnum.ISO3,
                                Lon = new List<object> { x.Longitude },
                                Lat = new List<object> { x.Latitude },
                                Mode = ModeFlag.Markers,
                                Text = $"{x.Label} ({x.ShortCode})",
                                Name = string.Empty,
                                HoverInfo = HoverInfoFlag.Lat | HoverInfoFlag.Lon,
                                HoverTemplate = $"%{{fullData.text}}<br />{nameof(HoverInfoFlag.Lat)}: %{{lat}}<br />{nameof(HoverInfoFlag.Lon)}: %{{lon}}"
                            }
                        ),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<ITrace>();
            }
        }

        private async Task<List<long>> CalculateNumberOfUniqueRoutes(int numberOfLocations, CancellationToken cancellationToken)
        {
            try
            {
                if (numberOfLocations < 1) return new List<long>();

                return await Task.Run(() =>
                    Enumerable.Range(1, numberOfLocations)
                        .Select(i => Enumerable.Range(1, i).Aggregate(1L, (f, x) => f * x))
                        .ToList(),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<long>();
            }
        }

        private async Task<List<LocationRow>> CalculateMatrix(List<LocationGeo> locations, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() =>
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
                                            DistanceInKilometers: (location, otherLocation) is { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } }
                                                ? lcGeo.GetDistanceTo(olcGeo)
                                                : 0D,
                                            Index: index,
                                            OrdinalFromOrigin: 0
                                        )
                                    )
                                    .OrderBy(x => x.DistanceInKilometers)
                                    .Select((x, ordinalFromOrigin) => (x with { OrdinalFromOrigin = ordinalFromOrigin }))
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
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);
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
