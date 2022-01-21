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

namespace HeuristicSearchMethodsSimulation.Services.TravelingSalesMan
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
        private IMongoCollection<LocationsCycle> LocationsCyclesCollection => Client.GetCollection<LocationsCycle>(MongoOptions.Databases.Data);

        public bool IsInit { get; private set; }
        public bool Progress { get; private set; }
        public List<LocationGeo> Locations { get; } = new();
        public bool HasLocations => !Locations.HasInsufficientLocations();
        public List<LocationGeo> LocationsBySelection { get; } = new();
        public TravelingSalesManAlgorithms Algorithm { get; private set; }
        public int MinSliderValue { get; private set; }
        public int MaxSliderValue { get; private set; }
        public int SliderStepValue { get; private set; }
        public int SliderValue { get; private set; }
        public bool RouteSymmetry { get; } = true;
        public ChartsOptions ChartsOptions { get; private set; } = new();
        private MapsOptions MapsOptions { get; set; } = new();
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

        public NoneItem? NoneItem { get; set; }
        public PreselectedItem? PreselectedItem { get; set; }
        public ExhaustiveItem? ExhaustiveItem { get; set; }
        public PartialRandomItem? PartialRandomItem { get; set; }
        public PartialImprovingItem? PartialImprovingItem { get; set; }
        public GuidedDirectItem? GuidedDirectItem { get; set; }
        public List<GuidedDirectPeripheralLocationsCycle> GuidedDirectLocationCycles { get; set; } = new();
        public EvolutionaryItem? EvolutionaryItem { get; set; }

        public Dictionary<int, List<HistoryIteration>> History { get; set; } = new();

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

        private async Task SetHistory(HistoryIteration obj)
        {
            if (!History.ContainsKey(SliderValue))
                History.Add(SliderValue, new());

            History[SliderValue] =
                await History[SliderValue]
                    .Append(obj)
                    .DistinctBy(x => x.Algo + x.Text)
                    .OrderBy(x => x.DistanceInKilometers)
                    .Take(Consts.MaxNumberOfHistoryLocations)
                    .ToListAsync()
                    .ConfigureAwait(true);
        }

        public async Task UpdateState(int sliderValue)
        {
            Progress = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdateState(sliderValue, Algorithm, _cts.Token),
                    Delay()
                })
                .ContinueWith(_ =>
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        private async Task SetDataFromDatabase()
        {
            var (locations, locationsCycles) = await Fetch(_fetchLimit, _cts.Token).ConfigureAwait(true);

            Locations.Clear();
            Locations.AddRange(locations);

            GuidedDirectLocationCycles.Clear();
            GuidedDirectLocationCycles.AddRange(
                await locationsCycles
                    .OfType<GuidedDirectPeripheralLocationsCycle>()
                    .OrderBy(x => x.NumberOfLocations)
                    .ToListAsync(_cts.Token)
                    .ConfigureAwait(true)
            );
        }

        public async Task Refresh()
        {
            Progress = true;
            OnStateChangeDelegate?.Invoke();

            await SetDataFromDatabase().ConfigureAwait(true);

            await Task.WhenAll(new[]
            {
                UpdateState(HasLocations ? SliderValue : _initialSliderValue, Algorithm, _cts.Token),
                Delay()
            })
            .ConfigureAwait(true);

            Progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public async Task Init(TravelingSalesManAlgorithms algo)
        {
            Algorithm = algo;

            if (_isInitializing) return;

            _isInitializing = true;

            Progress = true;

            OnStateChangeDelegate?.Invoke();

            if (!HasLocations)
                await SetDataFromDatabase().ConfigureAwait(true);

            await UpdateState(
                HasLocations ? SliderValue : _initialSliderValue, Algorithm,
                _cts.Token
            )
            .ConfigureAwait(true);

            IsInit = true;
            Progress = false;
            _isInitializing = false;
            OnStateChangeDelegate?.Invoke();
        }

        public Task SetExhaustiveItem(ExhaustiveIteration item) => SetExhaustiveItem(item, false, _cts.Token);

        private async Task SetExhaustiveItem(ExhaustiveIteration item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (ExhaustiveItem is not { }) return;

                if (!silent)
                {
                    Progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                if (item.Id == ExhaustiveItem.SelectedIteration?.Id)
                {
                    ExhaustiveItem.Matrix.Clear();
                    ExhaustiveItem.MapChartData.Clear();

                    ExhaustiveItem.Matrix.AddRange(ExhaustiveItem.ResetMatrix);
                    ExhaustiveItem.MapChartData.AddRange(ExhaustiveItem.MapMarkerData);
                    ExhaustiveItem.SelectedIteration = default;
                }
                else
                {
                    var cyclePairs = await item.Collection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var matrix = await ExhaustiveItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                    ExhaustiveItem.Matrix.Clear();
                    ExhaustiveItem.MapChartData.Clear();

                    ExhaustiveItem.Matrix.AddRange(matrix);
                    ExhaustiveItem.MapChartData.AddRange(mapLineData.Concat(ExhaustiveItem.MapMarkerData));
                    ExhaustiveItem.SelectedIteration = item;

                    await SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Exhaustive,
                            item.Text,
                            item.DistanceInKilometers,
                            new(ExhaustiveItem.MapChartData)
                        )
                    ).ConfigureAwait(true);
                }

                if (!silent)
                {
                    Progress = false;
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
                if (PartialRandomItem is not { }) return;

                Progress = true;
                OnStateChangeDelegate?.Invoke();

                PartialRandomItem.Matrix.Clear();
                PartialRandomItem.MapChartData.Clear();

                PartialRandomItem.Matrix.AddRange(PartialRandomItem.ResetMatrix);
                PartialRandomItem.MapChartData.AddRange(PartialRandomItem.MapMarkerData);
                PartialRandomItem.SelectedIteration = default;
                PartialRandomItem.Builder = default;

                if (LocationsBySelection.Count > 0)
                    await SetPartialRandomLocation(LocationsBySelection[0], true, cancellationToken).ConfigureAwait(true);

                Progress = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task SetPartialRandomLocation(Guid locationId)
        {
            if (LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location)
                await SetPartialRandomLocation(location, true, _cts.Token);
        }

        public Task SetPartialRandomLocation(LocationGeo item) => SetPartialRandomLocation(item, false, _cts.Token);

        private async Task SetPartialRandomLocation(LocationGeo item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (PartialRandomItem is not { }) return;

                if (!silent)
                {
                    Progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                if (PartialRandomItem.Builder is not { }) PartialRandomItem.Builder = new();

                PartialRandomItem.Builder.Collection[item.Id] = item with { };

                var collection = await PartialRandomItem.Builder.Collection.Values.ToListAsync(cancellationToken).ConfigureAwait(true);

                if (PartialRandomItem.Builder.Collection.Count == LocationsBySelection.Count)
                {
                    var totalDistance = await collection.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                    var text = collection.ToText();

                    var obj = new PartialRandomIteration(
                        collection,
                        text,
                        totalDistance,
                        Guid.NewGuid()
                    );

                    PartialRandomItem.Iterations.Add(obj);

                    await SetHistory(
                         new(
                             TravelingSalesManAlgorithms.Partial_Random,
                             text,
                             totalDistance,
                             await collection
                                .ToCyclePairs()
                                .ToMapLines()
                                .Concat(PartialRandomItem.MapMarkerData)
                                .ToListAsync(cancellationToken)
                                .ConfigureAwait(true)
                         )
                     ).ConfigureAwait(true);

                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();

                    PartialRandomItem.Matrix.AddRange(PartialRandomItem.ResetMatrix);
                    PartialRandomItem.MapChartData.AddRange(PartialRandomItem.MapMarkerData);

                    var locations = LocationsBySelection.Take(1).ToList();

                    var builder = new PartialRandomBuilderItem();

                    if (locations.Count > 0)
                    {
                        builder.Collection[locations[0].Id] = locations[0] with { };
                        builder.Text = locations.ToText(customLastElemText: "...");
                        builder.DistanceInKilometers = 0D;
                    }

                    builder.MapChartData.AddRange(PartialRandomItem.MapMarkerData);
                    builder.MapMarkerData.AddRange(PartialRandomItem.MapMarkerData);

                    PartialRandomItem.Builder = builder;
                }
                else
                {
                    var cyclePairs = await collection.ToPartialCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var totalDistance = await cyclePairs.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                    var text = collection.ToText(customLastElemText: "...");
                    var matrix = await PartialRandomItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();
                    PartialRandomItem.Builder.MapChartData.Clear();
                    PartialRandomItem.Builder.MapMarkerData.Clear();

                    PartialRandomItem.Matrix.AddRange(matrix);
                    PartialRandomItem.SelectedIteration = default;
                    PartialRandomItem.Builder.Text = text;
                    PartialRandomItem.Builder.DistanceInKilometers = totalDistance;
                    PartialRandomItem.Builder.MapChartData.AddRange(mapLineData.Concat(PartialRandomItem.MapMarkerData));
                    PartialRandomItem.Builder.MapMarkerData.AddRange(PartialRandomItem.MapMarkerData);

                    if (PartialRandomItem.Builder.Collection.Count == LocationsBySelection.Count - 1)
                    {
                        var autoItem = LocationsBySelection.ExceptBy(collection.Select(x => x.Id), x => x.Id).First();
                        await SetPartialRandomLocation(autoItem, true, cancellationToken).ConfigureAwait(true);
                    }
                }

                if (!silent)
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task SetPartialRandomItem(PartialRandomIteration item) => SetPartialRandomIteration(item, false, _cts.Token);

        private async Task SetPartialRandomIteration(PartialRandomIteration item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (PartialRandomItem is not { }) return;

                if (!silent)
                {
                    Progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                if (item.Id == PartialRandomItem.SelectedIteration?.Id)
                {
                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();

                    PartialRandomItem.Matrix.AddRange(PartialRandomItem.ResetMatrix);
                    PartialRandomItem.MapChartData.AddRange(PartialRandomItem.MapMarkerData);
                    PartialRandomItem.SelectedIteration = default;
                }
                else
                {
                    var cyclePairs = await item.Collection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                    var matrix = await PartialRandomItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();

                    PartialRandomItem.Matrix.AddRange(matrix);
                    PartialRandomItem.MapChartData.AddRange(mapLineData.Concat(PartialRandomItem.MapMarkerData));
                    PartialRandomItem.SelectedIteration = item;
                }

                if (!silent)
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task ResetPartialImproving()
        {
            if (PartialImprovingItem is not { }) return;

            Progress = true;

            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdatePartialImprovingState(
                        LocationsBySelection,
                        PartialImprovingItem.NumberOfUniqueRoutes,
                        PartialImprovingItem.ResetMatrix,
                        PartialImprovingItem.MapMarkerData,
                        TravelingSalesManAlgorithms.Partial_Improving,
                        _cts.Token
                    ),
                    Delay()
                })
                .ContinueWith(_ =>
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        public async Task PartialImprovingNextIteration()
        {
            try
            {
                if (PartialImprovingItem is not { })
                    return;

                if (PartialImprovingItem.Iterations.Count == 0 || PartialImprovingItem.Iteration >= PartialImprovingItem.Iterations.Count)
                {
                    PartialImprovingItem.CyclesMatch = true;
                    return;
                }

                var iteration = PartialImprovingItem.Iterations[PartialImprovingItem.Iteration];

                PartialImprovingItem.Matrix.Clear();
                PartialImprovingItem.MapChartData.Clear();

                PartialImprovingItem.Matrix.AddRange(iteration.Matrix);
                PartialImprovingItem.DistanceInKilometers = iteration.DistanceInKilometers;
                PartialImprovingItem.MapChartData.AddRange(iteration.MapLinesData.Concat(PartialImprovingItem.MapMarkerData));

                PartialImprovingItem.MapChartData.AddRange(iteration.MapLinesData.Concat(PartialImprovingItem.MapMarkerData));
                PartialImprovingItem.DistanceInKilometers = iteration.DistanceInKilometers;
                PartialImprovingItem.Log.Add(iteration.Log);
                PartialImprovingItem.Iteration++;

                if (PartialImprovingItem.Iteration >= PartialImprovingItem.Iterations.Count)
                {
                    PartialImprovingItem.CyclesMatch = true;

                    await SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Partial_Improving,
                            iteration.Text,
                            iteration.DistanceInKilometers,
                            PartialImprovingItem.MapChartData
                        )
                    ).ConfigureAwait(true);
                }

                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task ResetGuidedDirect()
        {
            if (GuidedDirectItem is not { }) return;

            Progress = true;

            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdateGuidedDirectState(
                        LocationsBySelection,
                        GuidedDirectItem.NumberOfUniqueRoutes,
                        GuidedDirectItem.ResetMatrix,
                        GuidedDirectItem.MapMarkerData,
                        TravelingSalesManAlgorithms.Guided_Direct,
                        _cts.Token
                    ),
                    Delay()
                })
                .ContinueWith(_ =>
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        public void SetGuidedDirectRule(int rule)
        {
            if (GuidedDirectItem is not { AllowRuleToggle: true } gdi || gdi.Rule == rule) return;

            gdi.Rule = rule;
        }

        public async Task SetGuidedDirectSelection(Guid locationId)
        {
            if (LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location)
                await SetGuidedDirectSelection(location).ConfigureAwait(true);
        }

        public async Task SetGuidedDirectSelection(LocationGeo location)
        {
            try
            {
                if (GuidedDirectItem is not { } gdi) return;

                if (gdi.Rule == 1) await HeadToClosestCityRule(gdi).ConfigureAwait(true);
                else if (gdi.Rule == 2) await PeripheralRule(gdi).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            async Task HeadToClosestCityRule(GuidedDirectItem gdi)
            {
                if (
                    gdi.HeadToClosestCity is not { Iterations.Count: > 0, Current: { } } htcc
                    || htcc.Index >= htcc.Iterations.Count
                    || !htcc.Iterations.Any(x => x.Node.Id == location.Id))
                {
                    return;
                }

                if (location.Id != htcc.Next?.Node.Id)
                {
                    if (!htcc.Log.Any(x => x.StartsWith(location.ShortCode)))
                    {
                        htcc.Log.Add($"{location.ShortCode} is not the best choice, please refer to the rule and try again.");
                        OnStateChangeDelegate?.Invoke();
                    }

                    return;
                }

                htcc.Index++;
                htcc.Visited[htcc.Current.Node.Id] = htcc.Current with { };

                if (htcc.Index == htcc.Solution.Count - 2)
                {
                    var autoItem = LocationsBySelection.ExceptBy(htcc.Visited.Values.Select(x => x.Node.Id), x => x.Id).First();
                    await SetGuidedDirectSelection(autoItem).ConfigureAwait(true);
                }
                else if (htcc.Index == htcc.Solution.Count - 1)
                {
                    htcc.Index++;
                    htcc.Text = $"{htcc.Current.Text} ({htcc.Current.DistanceInKilometers.ToFormattedDistance()})";
                    htcc.Log.Add("Congrats! No further improvements can be made while heading to the closest city.");
                    htcc.Completed = true;

                    if (gdi.Peripheral is { Iterations.Count: > 0 } p)
                    {
                        Progress = true;
                        OnStateChangeDelegate?.Invoke();

                        await Delay(2500).ConfigureAwait(true);

                        gdi.Rule = 2;
                        gdi.AllowRuleToggle = true;
                        Progress = false;
                        p.Log.Add("RULE 2: Head for the closest city, while sticking to an exterior route.");
                    }
                }

                OnStateChangeDelegate?.Invoke();
            }

            async Task PeripheralRule(GuidedDirectItem gdi)
            {
                if (
                    gdi.Peripheral is not { Iterations.Count: > 0, Current: { } } p
                    || p.Index >= p.Iterations.Count
                    || !p.Iterations.Any(x => x.Node.Id == location.Id))
                {
                    return;
                }

                if (p.Index == 0)
                {
                    if (p.IterationsLeft.Skip(1).FirstOrDefault()?.Node.Id == location.Id)
                        p.Left = true;
                    else if (p.IterationsRight.Skip(1).FirstOrDefault()?.Node.Id == location.Id)
                        p.Left = false;
                }

                if (location.Id != p.Next?.Node.Id)
                {
                    if (!p.Log.Any(x => x.StartsWith(location.ShortCode)))
                    {
                        p.Log.Add($"{location.ShortCode} is not the best choice, please refer to the rule and try again.");
                        OnStateChangeDelegate?.Invoke();
                    }

                    return;
                }

                p.Index++;
                p.Visited[p.Current.Node.Id] = p.Current with { };

                if (p.Index == p.Solution.Count - 2)
                {
                    var autoItem = LocationsBySelection.ExceptBy(p.Visited.Values.Select(x => x.Node.Id), x => x.Id).First();
                    await SetGuidedDirectSelection(autoItem).ConfigureAwait(true);
                }
                else if (p.Index == p.Solution.Count - 1)
                {
                    p.Index++;
                    p.Text = $"{p.Current.Text} ({p.Current.DistanceInKilometers.ToFormattedDistance()})";
                    p.Log.Add("Congrats! No further improvements can be made while sticking to an exterior route.");
                    p.Completed = true;
                }

                OnStateChangeDelegate?.Invoke();
            }
        }

        public async Task ResetEvolutionary()
        {
            if (EvolutionaryItem is not { }) return;

            Progress = true;
            OnStateChangeDelegate?.Invoke();

            UpdateEvolutionaryState(
                LocationsBySelection,
                EvolutionaryItem.NumberOfUniqueRoutes,
                EvolutionaryItem.ResetMatrix,
                EvolutionaryItem.MapMarkerData,
                TravelingSalesManAlgorithms.Evolutionary
            );

            await Delay().ConfigureAwait(true);

            Progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public async Task SetEvolutionaryStep(int? step = default)
        {
            try
            {
                if (EvolutionaryItem is not { }) return;

                Progress = true;
                OnStateChangeDelegate?.Invoke();

                await Delay().ConfigureAwait(true);

                EvolutionaryItem.Step = step ?? EvolutionaryItem.Step + 1;

                Progress = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task SetEvolutionaryLocation(EvolutionaryNode item) => SetEvolutionaryLocation(item, false, _cts.Token);
        public async Task SetEvolutionaryLocation(Guid locationId)
        {
            if (EvolutionaryItem?.Nodes.Skip(1).FirstOrDefault(x => x.Location.Id == locationId) is { } node)
                await SetEvolutionaryLocation(node, false, _cts.Token).ConfigureAwait(true);
        }

        private async Task SetEvolutionaryLocation(EvolutionaryNode item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (EvolutionaryItem is not { CycleComplete: false } || EvolutionaryItem.Visited.ContainsKey(item.Location.Id)) return;

                if (!silent)
                {
                    Progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                EvolutionaryItem.Generations[0].Nodes.Add(item with { });
                EvolutionaryItem.Generations[1].Nodes.Add(item with { });
                EvolutionaryItem.Visited[item.Location.Id] = item with { };

                if (
                    EvolutionaryItem.Visited.Count == LocationsBySelection.Count - 1 &&
                    EvolutionaryItem.Nodes.ExceptBy(EvolutionaryItem.Visited.Keys, x => x.Location.Id).FirstOrDefault() is { } last
                )
                {
                    EvolutionaryItem.Generations[0].Nodes.Add(last with { });
                    EvolutionaryItem.Generations[1].Nodes =
                        await EvolutionaryItem.Generations[1].Nodes
                            .Append(last with { })
                            .Skip(1)
                            .OrderBy(x => Random.Shared.Next())
                            .Prepend(EvolutionaryItem.Generations[1].Nodes[0])
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(true);
                    if (EvolutionaryItem.Generations[0].Nodes[1].Ordinal == EvolutionaryItem.Generations[1].Nodes[1].Ordinal)
                    {
                        var second = EvolutionaryItem.Generations[0].Nodes[1] with { };
                        EvolutionaryItem.Generations[1].Nodes.RemoveAt(1);
                        EvolutionaryItem.Generations[1].Nodes.Add(second);
                    }

                    EvolutionaryItem.Generations[1].DistanceInKilometers =
                        await EvolutionaryItem.Generations[1].Nodes
                            .ConvertAll(x => x.Location)
                            .CalculateDistanceOfCycle(cancellationToken)
                            .ConfigureAwait(true);

                    EvolutionaryItem.Offsprings.AddRange(
                        await EvolutionaryItem.Generations
                            .ComputeEvolutionaryOffsprings(EvolutionaryItem.NumberOfBitsOffspring, cancellationToken)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(true)
                    );

                    EvolutionaryItem.Visited[last.Location.Id] = last with { };
                }

                var collection =
                    await EvolutionaryItem.Visited.Values
                        .Select(x => x.Location)
                        .ToListAsync(cancellationToken).ConfigureAwait(true);
                var cycleComplete = EvolutionaryItem.Visited.Count == LocationsBySelection.Count;
                var cyclePairs =
                    await (
                        cycleComplete
                            ? collection.ToCyclePairs()
                            : collection.ToPartialCyclePairs()
                    )
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(true);
                var mapLineData = await cyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                var totalDistance = await cyclePairs.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                var matrix = await EvolutionaryItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                EvolutionaryItem.Matrix.Clear();
                EvolutionaryItem.DistanceInKilometers = default;
                EvolutionaryItem.MapChartData.Clear();

                EvolutionaryItem.Matrix.AddRange(matrix);
                EvolutionaryItem.DistanceInKilometers = totalDistance;
                EvolutionaryItem.MapChartData.AddRange(mapLineData.Concat(EvolutionaryItem.MapMarkerData));

                if (cycleComplete)
                {
                    EvolutionaryItem.Generations[0].DistanceInKilometers = totalDistance;
                    EvolutionaryItem.CycleComplete = true;
                }

                if (!silent)
                {
                    Progress = false;
                    OnStateChangeDelegate?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task Delay(int time = 250)
        {
            try
            {
                await Task.Delay(time, _cts.Token).ConfigureAwait(true);
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

        private async Task<(List<LocationGeo>, List<LocationsCycle>)> Fetch(int limit, CancellationToken cancellationToken)
        {
            Progress = true;

            try
            {
                var locations =
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

                var locationsCycles =
                    await LocationsCyclesCollection
                        .Find(Builders<LocationsCycle>.Filter.Empty)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);

                return (locations, locationsCycles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new();
            }
        }

        private async Task UpdateState(int sliderValue, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            SliderValue = sliderValue;
            Algorithm = algo;

            try
            {
                #region Calculate
                var locationsBySelection = Locations.Take(sliderValue).ToList();
                var matrix = await locationsBySelection.CalculateMatrix(cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutesPerNumberOfLocations = await CalculateNumberOfUniqueRoutesPerNumberOfLocations(sliderValue, cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutes = numberOfUniqueRoutesPerNumberOfLocations.LastOrDefault();
                var mapMarkerData = await CalculateMapMarkers(locationsBySelection, algo, cancellationToken).ConfigureAwait(true);
                #endregion

                #region Reset
                LocationsBySelection.Clear();
                #endregion

                #region Set
                LocationsBySelection.AddRange(locationsBySelection);

                UpdateNoneState(locationsBySelection, matrix, numberOfUniqueRoutes, numberOfUniqueRoutesPerNumberOfLocations, mapMarkerData, algo);
                await UpdatePreselectedState(locationsBySelection, matrix, mapMarkerData, algo, cancellationToken).ConfigureAwait(true);
                await UpdateExhaustiveState(locationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, algo, cancellationToken).ConfigureAwait(true);
                await UpdatePartialRandomState(locationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, algo, cancellationToken).ConfigureAwait(true);
                await UpdatePartialImprovingState(locationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, algo, cancellationToken).ConfigureAwait(true);
                await UpdateGuidedDirectState(locationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, algo, cancellationToken).ConfigureAwait(true);
                UpdateEvolutionaryState(locationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, algo);
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdateNoneState(
            List<LocationGeo> locations,
            List<LocationRow> matrix,
            long? numberOfUniqueRoutes,
            List<long> numberOfUniqueRoutesPerNumberOfLocations,
            List<ITrace> mapChartData,
            TravelingSalesManAlgorithms algo
        )
        {
            NoneItem = default;

            if (algo != TravelingSalesManAlgorithms.None) return;

            try
            {
                NoneItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };
                NoneItem.Matrix.AddRange(matrix);
                NoneItem.MapChartData.AddRange(mapChartData);

                if (locations.HasInsufficientLocations()) return;

                NoneItem.NumberOfUniqueRoutesPerNumberOfLocations.AddRange(numberOfUniqueRoutesPerNumberOfLocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdatePreselectedState(
            List<LocationGeo> locations,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo,
            CancellationToken cancellationToken
        )
        {
            PreselectedItem = default;

            if (algo != TravelingSalesManAlgorithms.Preselected) return;

            try
            {
                if (locations.HasInsufficientLocations())
                {
                    PreselectedItem = new() { NumberOfUniqueRoutes = 0 };
                    PreselectedItem.Matrix.AddRange(matrix);
                    PreselectedItem.MapChartData.AddRange(mapMarkerData);

                    return;
                }

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
                var totalDistance = await locations.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                var mapLineData = await locations.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);

                PreselectedItem = new()
                {
                    NumberOfUniqueRoutes = 1,
                    Text = locations.ToText(),
                    DistanceInKilometers = totalDistance
                };
                PreselectedItem.Matrix.AddRange(matrixWithHighlights);
                PreselectedItem.MapChartData.AddRange(mapLineData.Concat(mapMarkerData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdateExhaustiveState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo,
            CancellationToken cancellationToken
        )
        {
            ExhaustiveItem = default;

            if (algo != TravelingSalesManAlgorithms.Exhaustive) return;

            try
            {
                var maxExhaustiveLocationsToCalculateReached = locations.Count > _maxExhaustiveLocationsToCalculate;

                if (locations.HasInsufficientLocations() || maxExhaustiveLocationsToCalculateReached)
                {
                    ExhaustiveItem = new()
                    {
                        NumberOfUniqueRoutes = 0,
                        MaxExhaustiveLocationsToCalculateReached = maxExhaustiveLocationsToCalculateReached
                    };
                    ExhaustiveItem.Matrix.AddRange(matrix);
                    ExhaustiveItem.MapChartData.AddRange(mapMarkerData);

                    return;
                }

                ExhaustiveItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };

                var iterations = await CalculateExhaustiveIterations(locations, cancellationToken).ConfigureAwait(true);

                ExhaustiveItem.ResetMatrix.AddRange(matrix);
                ExhaustiveItem.Iterations.AddRange(iterations);
                ExhaustiveItem.MapMarkerData.AddRange(mapMarkerData);

                if (iterations.Count == 1)
                {
                    await SetExhaustiveItem(iterations[0], true, cancellationToken).ConfigureAwait(true);
                    return;
                }

                ExhaustiveItem.Matrix.AddRange(matrix);
                ExhaustiveItem.MapChartData.AddRange(mapMarkerData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdatePartialRandomState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo,
            CancellationToken cancellationToken
        )
        {
            PartialRandomItem = default;

            if (algo != TravelingSalesManAlgorithms.Partial_Random) return;

            try
            {
                PartialRandomItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };

                if (locations.HasInsufficientLocations())
                {
                    PartialRandomItem.Matrix.AddRange(matrix);
                    PartialRandomItem.MapChartData.AddRange(mapMarkerData);

                    return;
                }

                PartialRandomItem.ResetMatrix.AddRange(matrix);
                PartialRandomItem.MapMarkerData.AddRange(mapMarkerData);

                await SetPartialRandomLocation(locations[0], true, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdatePartialImprovingState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo,
            CancellationToken cancellationToken
        )
        {
            PartialImprovingItem = default;

            if (algo != TravelingSalesManAlgorithms.Partial_Improving) return;

            try
            {
                PartialImprovingItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };

                if (locations.HasInsufficientLocations())
                {
                    PartialImprovingItem.Matrix.AddRange(matrix);
                    PartialImprovingItem.MapChartData.AddRange(mapMarkerData);

                    return;
                }

                PartialImprovingItem.ResetMatrix.AddRange(matrix);
                PartialImprovingItem.MapMarkerData.AddRange(mapMarkerData);

                var iterations =
                    await locations
                        .ComputePartialImprovingIterations(matrix, MapsOptions.PartialImproving, cancellationToken)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);

                var computed = iterations[0] with { };

                if (iterations.Count > 1 && DateTimeOffset.Now.Millisecond % 2 == 0)
                {
                    var random = new Random().Next(0, iterations.Count - 1);

                    iterations = iterations.Skip(random).ToList();
                }

                var cyclesMatch = iterations.Count == 1 || locations.Count == Consts.MinNumberOfLocations;

                if (cyclesMatch)
                    computed = iterations[0] with { Log = "Congrats in finding an optimal route on your first attempt!" };

                var computedCollection = computed.Collection;
                var computedCycle = computed.Cycle;
                var totalDistance = computed.DistanceInKilometers;
                var highlightedMatrix = computed.Matrix;
                var mapLinesData = computed.MapLinesData;
                var text = computedCollection.ToText();

                PartialImprovingItem.Matrix.AddRange(highlightedMatrix);
                PartialImprovingItem.DistanceInKilometers = totalDistance;
                PartialImprovingItem.Text = text;
                PartialImprovingItem.Iterations.AddRange(iterations.Skip(1));
                PartialImprovingItem.ComputedCollection.AddRange(computedCollection);
                PartialImprovingItem.ComputedCycle.AddRange(computedCycle);
                PartialImprovingItem.MapChartData.AddRange(mapLinesData.Concat(mapMarkerData));
                PartialImprovingItem.CyclesMatch = cyclesMatch;
                PartialImprovingItem.Log.Add(computed.Log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdateGuidedDirectState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo,
            CancellationToken cancellationToken
        )
        {
            GuidedDirectItem = default;

            if (algo != TravelingSalesManAlgorithms.Guided_Direct) return;

            try
            {
                GuidedDirectItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };

                GuidedDirectItem.HeadToClosestCity.Matrix.AddRange(matrix);
                GuidedDirectItem.HeadToClosestCity.ResetMatrix.AddRange(matrix);
                GuidedDirectItem.HeadToClosestCity.MapChartData.AddRange(mapMarkerData);
                GuidedDirectItem.HeadToClosestCity.MapMarkerData.AddRange(mapMarkerData);
                GuidedDirectItem.Peripheral.Matrix.AddRange(matrix);
                GuidedDirectItem.Peripheral.ResetMatrix.AddRange(matrix);
                GuidedDirectItem.Peripheral.MapChartData.AddRange(mapMarkerData);
                GuidedDirectItem.Peripheral.MapMarkerData.AddRange(mapMarkerData);

                if (locations.HasInsufficientLocations()) return;

                GuidedDirectItem.Log.Add("RULE 1: Always head for the closest city.");

                var headToClosestCityCollection =
                    await matrix
                        .ComputeHeadToClosestCityGuidedDirectCollection()
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);
                var headToClosestCityIterations =
                    await headToClosestCityCollection
                        .ComputeGuidedDirectIterationsFromGuidedDirectCollection(matrix, mapMarkerData, cancellationToken)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);

                GuidedDirectItem.HeadToClosestCity.Solution = headToClosestCityCollection;
                GuidedDirectItem.HeadToClosestCity.Visited.Add(headToClosestCityIterations[0].Node.Id, headToClosestCityIterations[0]);
                GuidedDirectItem.HeadToClosestCity.Iterations.AddRange(headToClosestCityIterations);

                var firstItem = locations[0] with { };
                var peripheralCycleForNumberOfCities =
#pragma warning disable RCS1077 // Optimize LINQ method call.
                    GuidedDirectLocationCycles
                        .FirstOrDefault(x =>
                            x.Collection.FirstOrDefault() == firstItem.Id
                            && x.NumberOfLocations == locations.Count
                        )?.Collection
                    ?? new List<Guid>();
#pragma warning restore RCS1077 // Optimize LINQ method call.

                if (peripheralCycleForNumberOfCities.Count == 0) return;

                var peripheralCollection =
                    await peripheralCycleForNumberOfCities
                        .Join(
                            LocationsBySelection,
                            s => s,
                            l => l.Id,
                            (_, l) => l
                        )
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);
                var peripheralIterationsLeft =
                   await peripheralCollection
                       .Append(firstItem)
                       .Reverse()
                       .SkipLast(1)
                       .ToList()
                       .ComputeGuidedDirectIterationsFromGuidedDirectCollection(matrix, mapMarkerData, cancellationToken)
                       .ToListAsync(cancellationToken)
                       .ConfigureAwait(true);
                var peripheralIterationsRight =
                    await peripheralCollection
                        .ComputeGuidedDirectIterationsFromGuidedDirectCollection(matrix, mapMarkerData, cancellationToken)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);

                GuidedDirectItem.Peripheral.Solution = peripheralCollection;
                GuidedDirectItem.Peripheral.Visited.Add(peripheralIterationsRight[0].Node.Id, peripheralIterationsRight[0]);
                GuidedDirectItem.Peripheral.IterationsLeft.AddRange(peripheralIterationsLeft);
                GuidedDirectItem.Peripheral.IterationsRight.AddRange(peripheralIterationsRight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdateEvolutionaryState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo
        )
        {
            EvolutionaryItem = default;

            if (algo != TravelingSalesManAlgorithms.Evolutionary) return;

            try
            {
                EvolutionaryItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes, DistanceInKilometers = 0D };

                EvolutionaryItem.Matrix.AddRange(matrix);
                EvolutionaryItem.ResetMatrix.AddRange(matrix);
                EvolutionaryItem.MapChartData.AddRange(mapMarkerData);
                EvolutionaryItem.MapMarkerData.AddRange(mapMarkerData);

                if (locations.HasInsufficientLocations()) return;

                EvolutionaryItem.NumberOfBitsOffspring = LocationsBySelection.Count switch
                {
                    < 5 => 2,
                    < 7 => 3,
                    _ => 4
                };
                EvolutionaryItem.Nodes.AddRange(locations.Select((x, i) => new EvolutionaryNode(i, x)));
                var first = EvolutionaryItem.Nodes[0];
                EvolutionaryItem.Generations.AddRange(
                    new EvolutionaryNodes[]
                    {
                        new() { Nodes = new() { first with { } } },
                        new() { Nodes = new() { first with { } } }
                    }
                );
                EvolutionaryItem.Visited[first.Location.Id] = first with { };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
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
                    TravelingSalesManAlgorithms.Evolutionary => Evolutionary(locations, MapsOptions, cancellationToken),
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
                                    Color = options.Evolutionary.FirstMarkerColor,
                                    Symbol = options.Evolutionary.FirstMarkerSymbol
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

        private async Task<List<ExhaustiveIteration>> CalculateExhaustiveIterations(List<LocationGeo> locations, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.HasInsufficientLocations()) return new();

                return await locations
                    .CalculatePermutations(locations[0].Id)
                    .Select(collection => new ExhaustiveIteration(collection, collection.ToText(), collection.CalculateDistanceOfCycle(), Guid.NewGuid()))
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
