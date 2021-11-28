using AutoMapper;
using GeoCoordinatePortable;
using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Extensions;
using HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
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
using Location = HeuristicSearchMethodsSimulation.Models.TravelingSalesMan.Location;

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
        private ChartsOptions _chartOptions = new();
        private MapsOptions _mapsOptions = new();
        private int _maxExhaustiveLocationsToCalculate = 7;

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
        public TravelingSalesManAlgorithms Algorithm { get; private set; }
        public int MinSliderValue { get; private set; }
        public int MaxSliderValue { get; private set; }
        public int SliderStepValue { get; private set; }
        public int SliderValue { get; private set; }
        public bool RouteSymmetry { get; } = true;
        public double? TotalDistanceInKilometers { get; private set; }
        public ChartsOptions ChartsOptions => _chartOptions;
        public List<ITrace> MapChartData { get; } = new();
        public List<ITrace> MapMarkerData { get; } = new();
        public List<ITrace> MapLinesData { get; } = new();
        public MapOptions MapOptions { get; private set; } = new();
        public string? PreselectedCycleText { get; private set; }
        public bool MaxExhaustiveLocationsToCalculateReached => LocationsBySelection.Count > _maxExhaustiveLocationsToCalculate;
        public List<ExhaustiveItem> ExhaustiveItems { get; } = new();
        public ExhaustiveItem? SelectedExhaustiveItem { get; private set; }
        public List<PartialRandomItem> PartialRandomItems { get; } = new();
        public PartialRandomItem? SelectedPartialRandomItem { get; private set; }
        public PartialRandomBuilderItem? PartialRandomBuilderItem { get; set; }
        public PartialImprovingItem? PartialImprovingItem { get; set; }

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

        public async Task UpdateState(int sliderValue)
        {
            Loading = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdateState(sliderValue, Algorithm, _cts.Token),
                    Delay()
                })
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

            await Task.WhenAll(new[]
                {
                    UpdateState(HasLocations ? SliderValue : _initialSliderValue, Algorithm, _cts.Token),
                    Delay()
                })
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

        private async Task<List<LocationRow>> ResetMatrix(CancellationToken cancellationToken) =>
            await Matrix
                .Select((row, rowIndex) => row with
                {
                    Collection =
                        row.Collection
                            .Select((cell, cellIndex) => cell with
                            {
                                IsHighlightedDistance = false
                            })
                            .ToList()
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);

        public Task SetExhaustiveItem(ExhaustiveItem item) => SetExhaustiveItem(item, false, _cts.Token);

        private async Task SetExhaustiveItem(ExhaustiveItem item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (!silent)
                {
                    Loading = true;

                    OnStateChangeDelegate?.Invoke();
                }

                if (item.Id == SelectedExhaustiveItem?.Id)
                {
                    var resetMatrix = await ResetMatrix(cancellationToken).ConfigureAwait(true);

                    Matrix.Clear();
                    MapLinesData.Clear();
                    MapChartData.Clear();

                    Matrix.AddRange(resetMatrix);
                    TotalDistanceInKilometers = default;
                    MapChartData.AddRange(MapMarkerData);
                    SelectedExhaustiveItem = default;
                }
                else
                {
                    var cyclePairs = await item.Collection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var matrix =
                        await Matrix
                            .Select((row, rowIndex) => row with
                            {
                                Collection =
                                    row.Collection
                                        .Select((cell, cellIndex) => cell with
                                        {
                                            IsHighlightedDistance = cyclePairs.Any(pair => cell.A.Id == pair.A.Id && cell.B.Id == pair.B.Id)
                                        })
                                        .ToList()
                            })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(true);

                    Matrix.Clear();
                    MapLinesData.Clear();
                    MapChartData.Clear();

                    Matrix.AddRange(matrix);
                    TotalDistanceInKilometers = item.DistanceInKilometers;
                    MapChartData.AddRange(mapLineData.Concat(MapMarkerData));
                    MapLinesData.AddRange(mapLineData);
                    SelectedExhaustiveItem = item;
                }

                if (!silent)
                {
                    Loading = false;

                    OnStateChangeDelegate?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task ClearPartialRandomBuilder() => ClearPartialRandomBuilder(_cts.Token);

        private async Task ClearPartialRandomBuilder(CancellationToken cancellationToken)
        {
            try
            {
                Loading = true;
                OnStateChangeDelegate?.Invoke();

                var matrix = await ResetMatrix(cancellationToken).ConfigureAwait(true);

                Matrix.Clear();
                MapLinesData.Clear();
                MapChartData.Clear();

                Matrix.AddRange(matrix);
                TotalDistanceInKilometers = default;
                MapChartData.AddRange(MapMarkerData);
                SelectedPartialRandomItem = default;
                PartialRandomBuilderItem = default;

                Loading = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task SetPartialRandomLocation(LocationGeo item) => SetPartialRandomLocation(item, _cts.Token);

        public async Task SetPartialRandomLocation(LocationGeo item, CancellationToken cancellationToken)
        {
            try
            {
                Loading = true;

                OnStateChangeDelegate?.Invoke();

                if (PartialRandomBuilderItem is null) PartialRandomBuilderItem = new();

                PartialRandomBuilderItem.Collection[item.Id] = item;

                var collection = await PartialRandomBuilderItem.Collection.Values.ToListAsync(cancellationToken).ConfigureAwait(true);

                if (PartialRandomBuilderItem.Collection.Count == LocationsBySelection.Count)
                {
                    var totalDistance = await collection.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);

                    var obj = new PartialRandomItem(
                        collection,
                        collection.ToText(),
                        totalDistance,
                        Guid.NewGuid()
                    );
                    PartialRandomItems.Add(obj);

                    PartialRandomBuilderItem = default;

                    await SetPartialRandomItem(obj, true, cancellationToken).ConfigureAwait(true);
                }
                else
                {
                    var cyclePairs = await collection.ToPartialCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var totalDistance = await cyclePairs.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                    var text = collection.ToText(customLastElemText: "...");
                    var matrix =
                        await Matrix
                            .Select((row, rowIndex) => row with
                            {
                                Collection =
                                    row.Collection
                                        .Select((cell, cellIndex) => cell with
                                        {
                                            IsHighlightedDistance = cyclePairs.Any(pair => cell.A.Id == pair.A.Id && cell.B.Id == pair.B.Id)
                                        })
                                        .ToList()
                            })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(true);

                    Matrix.Clear();
                    MapLinesData.Clear();
                    MapChartData.Clear();
                    PartialRandomBuilderItem.MapChartData.Clear();
                    PartialRandomBuilderItem.MapMarkerData.Clear();
                    PartialRandomBuilderItem.MapLinesData.Clear();

                    Matrix.AddRange(matrix);

                    SelectedPartialRandomItem = default;
                    PartialRandomBuilderItem.Text = text;
                    PartialRandomBuilderItem.DistanceInKilometers = totalDistance;
                    PartialRandomBuilderItem.MapChartData.AddRange(mapLineData.Concat(MapMarkerData));
                    PartialRandomBuilderItem.MapMarkerData.AddRange(MapMarkerData);
                    PartialRandomBuilderItem.MapLinesData.AddRange(mapLineData);
                }

                Loading = false;

                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task SetPartialRandomItem(PartialRandomItem item) => SetPartialRandomItem(item, false, _cts.Token);

        private async Task SetPartialRandomItem(PartialRandomItem item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (!silent)
                {
                    Loading = true;

                    OnStateChangeDelegate?.Invoke();
                }

                if (item.Id == SelectedPartialRandomItem?.Id)
                {
                    var resetMatrix = await ResetMatrix(cancellationToken).ConfigureAwait(true);

                    Matrix.Clear();
                    MapLinesData.Clear();
                    MapChartData.Clear();

                    Matrix.AddRange(resetMatrix);
                    TotalDistanceInKilometers = default;
                    MapChartData.AddRange(MapMarkerData);
                    SelectedPartialRandomItem = default;
                }
                else
                {
                    var cyclePairs = await item.Collection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var matrix =
                        await Matrix
                            .Select((row, rowIndex) => row with
                            {
                                Collection =
                                    row.Collection
                                        .Select((cell, cellIndex) => cell with
                                        {
                                            IsHighlightedDistance = cyclePairs.Any(pair => cell.A.Id == pair.A.Id && cell.B.Id == pair.B.Id)
                                        })
                                        .ToList()
                            })
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(true);

                    Matrix.Clear();
                    MapLinesData.Clear();
                    MapChartData.Clear();

                    Matrix.AddRange(matrix);
                    TotalDistanceInKilometers = item.DistanceInKilometers;
                    MapChartData.AddRange(mapLineData.Concat(MapMarkerData));
                    MapLinesData.AddRange(mapLineData);
                    SelectedPartialRandomItem = item;
                }

                if (!silent)
                {
                    Loading = false;

                    OnStateChangeDelegate?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
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
                _chartOptions = TravelingSalesManOptions.Charts;
                _mapsOptions = TravelingSalesManOptions.Maps;
                _maxExhaustiveLocationsToCalculate = TravelingSalesManOptions.MaxExhaustiveLocationsToCalculate;
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

                return new();
            }
        }

        private MapOptions ResolveMapOptions(TravelingSalesManAlgorithms algo) => algo switch
        {
            TravelingSalesManAlgorithms.Evolutionary => _mapsOptions.Evolutionary,
            TravelingSalesManAlgorithms.Exhaustive => _mapsOptions.Exhaustive,
            TravelingSalesManAlgorithms.Guided_Direct => _mapsOptions.GuidedDirect,
            TravelingSalesManAlgorithms.None => _mapsOptions.None,
            TravelingSalesManAlgorithms.Preselected => _mapsOptions.Preselected,
            TravelingSalesManAlgorithms.Partial_Improving => _mapsOptions.PartialImproving,
            TravelingSalesManAlgorithms.Partial_Random => _mapsOptions.PartialRandom,
            _ => _mapsOptions.Default
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
                #region Calculate
                var locationsBySelection = Locations.Take(sliderValue).ToList();
                var matrix = await CalculateMatrix(locationsBySelection, cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutesPerNumberOfLocations = await CalculateNumberOfUniqueRoutesPerNumberOfLocations(sliderValue, cancellationToken).ConfigureAwait(true);
                var mapMarkerData = await CalculateMapMarkers(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);
                #endregion

                #region Reset
                LocationsBySelection.Clear();
                Matrix.Clear();
                NumberOfUniqueRoutesPerNumberOfLocations.Clear();
                MapChartData.Clear();
                MapMarkerData.Clear();
                MapLinesData.Clear();
                #endregion

                #region Set
                LocationsBySelection.AddRange(locationsBySelection);
                NumberOfUniqueRoutesPerNumberOfLocations.AddRange(numberOfUniqueRoutesPerNumberOfLocations);
                MapChartData.AddRange(mapMarkerData);
                MapMarkerData.AddRange(mapMarkerData);

                UpdateNoneState(matrix, algo);
                await UpdatePreselectedState(locationsBySelection, matrix, algo, cancellationToken).ConfigureAwait(true);
                await UpdateExhaustiveState(locationsBySelection, matrix, algo, cancellationToken).ConfigureAwait(true);
                UpdatePartialRandomState(matrix, algo);
                await UpdatePartialImprovingState(locationsBySelection, matrix, algo, cancellationToken).ConfigureAwait(true);

                UpdateOtherStates(matrix, algo);
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdateOtherStates(List<LocationRow> matrix, TravelingSalesManAlgorithms algo)
        {
            if (algo switch
            {
                TravelingSalesManAlgorithms.Guided_Direct => false,
                TravelingSalesManAlgorithms.Evolutionary => false,
                _ => true
            })
            {
                return;
            }

            Matrix.AddRange(matrix);
            TotalDistanceInKilometers = 0D;
        }

        private void UpdateNoneState(List<LocationRow> matrix, TravelingSalesManAlgorithms algo)
        {
            if (algo != TravelingSalesManAlgorithms.None) return;

            Matrix.AddRange(matrix);
            TotalDistanceInKilometers = default;
        }

        private async Task UpdatePreselectedState(List<LocationGeo> locations, List<LocationRow> matrix, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            PreselectedCycleText = default;

            if (algo != TravelingSalesManAlgorithms.Preselected) return;

            var matrixWithHighlights = matrix switch
            {
                { Count: >= Consts.MinNumberOfLocations } =>
                    await matrix
                        .Select((row, rowIndex) =>
                            row with
                            {
                                Collection =
                                    row.Collection
                                        .Select((cell, cellIndex) =>
                                            rowIndex + 1 == cellIndex
                                            || (rowIndex + 1 == row.Collection.Count && cellIndex == 0)
                                                ? cell with { IsHighlightedDistance = true }
                                                : cell
                                        )
                                        .ToList()
                            }
                        )
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true),
                _ => matrix
            };
            var totalDistance =
                locations.HasInsufficientLocations()
                    ? default(double?)
                    : await locations.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
            var mapLineData = await locations.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
            var text = locations.ToText();

            Matrix.AddRange(matrixWithHighlights);
            TotalDistanceInKilometers = totalDistance;
            MapChartData.InsertRange(0, mapLineData);
            MapLinesData.AddRange(mapLineData);
            PreselectedCycleText = text;
        }

        private async Task UpdateExhaustiveState(List<LocationGeo> locations, List<LocationRow> matrix, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            ExhaustiveItems.Clear();
            SelectedExhaustiveItem = default;

            if (algo != TravelingSalesManAlgorithms.Exhaustive) return;

            var exhaustiveItems = await CalculateExhaustiveItems(locations, algo, cancellationToken).ConfigureAwait(true);

            Matrix.AddRange(matrix);
            TotalDistanceInKilometers = default;
            ExhaustiveItems.AddRange(exhaustiveItems);

            if (ExhaustiveItems.Count == 1)
                await SetExhaustiveItem(ExhaustiveItems[0], true, cancellationToken).ConfigureAwait(true);
        }

        private void UpdatePartialRandomState(List<LocationRow> matrix, TravelingSalesManAlgorithms algo)
        {
            PartialRandomItems.Clear();
            SelectedPartialRandomItem = default;
            PartialRandomBuilderItem = default;

            if (algo != TravelingSalesManAlgorithms.Partial_Random) return;

            Matrix.AddRange(matrix);
            TotalDistanceInKilometers = default;
        }

        private async Task UpdatePartialImprovingState(List<LocationGeo> locations, List<LocationRow> matrix, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            PartialImprovingItem = default;

            if (algo != TravelingSalesManAlgorithms.Partial_Improving) return;

            PartialImprovingItem = new();

            Matrix.AddRange(matrix);
            TotalDistanceInKilometers = default;
        }

        private async Task<List<ITrace>> CalculateMapMarkers(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.Count == 0)
                    return new() { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };

                return await (algo switch
                {
                    TravelingSalesManAlgorithms.Preselected => Preselected(locations, cancellationToken),
                    TravelingSalesManAlgorithms.Evolutionary => Evolutionary(locations, _mapsOptions, cancellationToken),
                    _ => Default(locations, cancellationToken)
                })
                .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new() { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };
            }

            static async Task<List<ITrace>> Evolutionary(List<LocationGeo> locations, MapsOptions options, CancellationToken cancellationToken) =>
                await locations
                    .Select((x, i) =>
                        i > 0
                            ? new ScatterGeo
                            {
                                LocationMode = LocationModeEnum.ISO3,
                                Lon = new List<object> { x.Longitude },
                                Lat = new List<object> { x.Latitude },
                                Mode = ModeFlag.Markers | ModeFlag.Text,
                                Marker = new()
                                {
                                    Color = options.Evolutionary.MarkerColor,
                                    Symbol = options.Evolutionary.MarkerSymbol
                                },
                                Text = $"{i} - {x.ShortCode}",
                                TextPosition = TextPositionEnum.TopCenter,
                                Name = $"{x.Label} ({x.ShortCode})",
                                HoverLabel = new() { NameLength = 0 },
                                HoverTemplate = $"Ordinal: {i}<br />{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                                Meta = x.Id
                            }
                            : new ScatterGeo
                            {
                                LocationMode = LocationModeEnum.ISO3,
                                Lon = new List<object> { x.Longitude },
                                Lat = new List<object> { x.Latitude },
                                Mode = ModeFlag.Markers | ModeFlag.Text,
                                Marker = new()
                                {
                                    Color = options.Evolutionary.MarkerColor,
                                    Symbol = options.Default.MarkerSymbol
                                },
                                Text = x.ShortCode,
                                TextPosition = TextPositionEnum.TopCenter,
                                Name = $"{x.Label} ({x.ShortCode})",
                                HoverLabel = new() { NameLength = 0 },
                                HoverTemplate = $"{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                                Meta = x.Id
                            }
                    )
                    .ToListAsync<ITrace>(cancellationToken)
                    .ConfigureAwait(true);

            static async Task<List<ITrace>> Preselected(List<LocationGeo> locations, CancellationToken cancellationToken) =>
                await locations
                    .Select((x, i) =>
                        new ScatterGeo
                        {
                            LocationMode = LocationModeEnum.ISO3,
                            Lon = new List<object> { x.Longitude },
                            Lat = new List<object> { x.Latitude },
                            Mode = ModeFlag.Markers | ModeFlag.Text,
                            Text = x.ShortCode,
                            TextPosition = TextPositionEnum.TopCenter,
                            Name = $"{x.Label} ({x.ShortCode})",
                            HoverLabel = new() { NameLength = 0 },
                            HoverTemplate = $"Ordinal: {i + 1}<br />{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                            Meta = x.Id
                        }
                    )
                    .ToListAsync<ITrace>(cancellationToken)
                    .ConfigureAwait(true);

            static async Task<List<ITrace>> Default(List<LocationGeo> locations, CancellationToken cancellationToken) =>
                await locations
                    .Select(x =>
                        new ScatterGeo
                        {
                            LocationMode = LocationModeEnum.ISO3,
                            Lon = new List<object> { x.Longitude },
                            Lat = new List<object> { x.Latitude },
                            Mode = ModeFlag.Markers | ModeFlag.Text,
                            Text = x.ShortCode,
                            TextPosition = TextPositionEnum.TopCenter,
                            Name = $"{x.Label} ({x.ShortCode})",
                            HoverLabel = new() { NameLength = 0 },
                            HoverTemplate = $"{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                            Meta = x.Id
                        }
                    )
                    .ToListAsync<ITrace>(cancellationToken)
                    .ConfigureAwait(true);
        }

        private static async Task<List<long>> CalculateNumberOfUniqueRoutesPerNumberOfLocations(int numberOfLocations, CancellationToken cancellationToken) =>
            await Enumerable.Range(0, numberOfLocations)
                .Select(i => Enumerable.Range(1, i).Aggregate(1L, (f, x) => f * x) / 2) // (n − 1)! / 2
                .Take(numberOfLocations)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);

        private async Task<List<LocationRow>> CalculateMatrix(List<LocationGeo> locations, CancellationToken cancellationToken)
        {
            try
            {
                return await locations
                    .Select(location =>
                    {
                        var rowCollection =
                            locations
                                .Select((otherLocation, index) =>
                                    new LocationToLocation(
                                        A: location,
                                        B: otherLocation,
                                        DirectionalKey: location.ToDirectionalKey(otherLocation),
                                        ReverseDirectionalKey: location.ToReverseDirectionalKey(otherLocation),
                                        Key: location.ToKey(otherLocation),
                                        DistanceInKilometers: location.CalculateDistancePointToPointInKilometers(otherLocation),
                                        Index: index,
                                        IsHighlightedDistance: false
                                    )
                                )
                                .ToList();

                        return new LocationRow(
                            Collection: rowCollection,
                            Ylabel: $"{location.Label} ({location.ShortCode})",
                            Xlabels: locations.ConvertAll(x => $"{x.Label} ({x.ShortCode})")
                        );
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new();
            }
        }

        private async Task<List<ExhaustiveItem>> CalculateExhaustiveItems(List<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (
                    locations.HasInsufficientLocations() ||
                    locations.Count > _maxExhaustiveLocationsToCalculate ||
                    algo != TravelingSalesManAlgorithms.Exhaustive
                )
                {
                    return new();
                }

                return await Permute(locations)
                    .Select(collection => new ExhaustiveItem(collection, collection.ToText(), collection.CalculateDistanceOfCycle(), Guid.NewGuid()))
                    .GroupBy(x => x.DistanceInKilometers.ToFormattedDistance())
                    .Select(x => x.First())
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new();
            }
        }

        private static IEnumerable<List<LocationGeo>> Permute(List<LocationGeo> set)
        {
            var count = set.Count;
            var a = new List<int>();
            var p = new List<int>();

            var list = new List<LocationGeo>(set);

            int i, j, tmp;

            for (i = 0; i < count; i++)
            {
                a.Insert(i, i + 1);
                p.Insert(i, 0);
            }

            yield return list;

            i = 1;

            while (i < count)
            {
                if (p[i] < i)
                {
                    j = i % 2 * p[i];

                    tmp = a[j];
                    a[j] = a[i];
                    a[i] = tmp;

                    var yieldRet = new List<LocationGeo>();

                    for (int x = 0; x < count; x++)
                        yieldRet.Insert(x, list[a[x] - 1]);

                    yield return yieldRet;

                    p[i]++;
                    i = 1;
                }
                else
                {
                    p[i] = 0;
                    i++;
                }
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
