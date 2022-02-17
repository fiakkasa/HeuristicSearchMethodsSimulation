using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.Extensions;
using HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
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

namespace HeuristicSearchMethodsSimulation.Services.TravelingSalesMan
{
    public class TravelingSalesManService :
        IDisposable,
        ITravelingSalesManNoneService,
        ITravelingSalesManPreselectedService,
        ITravelingSalesManExhaustiveService,
        ITravelingSalesManPartialRandomService,
        ITravelingSalesManPartialImprovingService,
        ITravelingSalesManGuidedDirectService,
        ITravelingSalesManEvolutionaryService
    {
        private bool _disposedValue;
        private readonly ITravelingSalesManFoundationService _travelingSalesManFoundationService;
        private readonly ITravelingSalesManHistoryService _travelingSalesManHistoryService;
        private readonly ILogger<TravelingSalesManService> _logger;
        private readonly CancellationTokenSource _cts = new();
        private bool _isInit;
        private bool _progress;

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

        public bool IsInit => _isInit || _travelingSalesManFoundationService.IsInit;
        public bool Progress => _progress || _travelingSalesManFoundationService.Progress;

        private IMongoCollection<LocationsCycle> LocationsCyclesCollection =>
            _travelingSalesManFoundationService.Client.GetCollection<LocationsCycle>(_travelingSalesManFoundationService.DatabaseName);

        public NoneItem? NoneItem { get; set; }
        public PreselectedItem? PreselectedItem { get; set; }
        public ExhaustiveItem? ExhaustiveItem { get; set; }
        public PartialRandomItem? PartialRandomItem { get; set; }
        public PartialImprovingItem? PartialImprovingItem { get; set; }
        public GuidedDirectItem? GuidedDirectItem { get; set; }
        public List<GuidedDirectPeripheralLocationsCycle> GuidedDirectLocationCycles { get; set; } = new();
        public EvolutionaryItem? EvolutionaryItem { get; set; }

        public TravelingSalesManService(
            ITravelingSalesManFoundationService travelingSalesManFoundationService,
            ITravelingSalesManHistoryService travelingSalesManHistoryService,
            ILogger<TravelingSalesManService> logger
        )
        {
            _travelingSalesManFoundationService = travelingSalesManFoundationService;
            _travelingSalesManHistoryService = travelingSalesManHistoryService;
            _logger = logger;
            _travelingSalesManFoundationService.OnInitComplete += PostInitCompleteHandler();
            _travelingSalesManFoundationService.OnProgress += OnProgressHandler();
        }

        private Action OnProgressHandler() => () => OnStateChangeDelegate?.Invoke();

        public async Task UpdateState(int sliderValue)
        {
            _progress = true;
            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdateState(sliderValue, _cts.Token),
                    Delay()
                })
                .ContinueWith(_ =>
                {
                    _progress = false;
                    OnStateChangeDelegate?.Invoke();
                })
                .ConfigureAwait(true);
        }

        private async Task SetLocationsCycleDataFromDatabase(CancellationToken cancellationToken)
        {
            try
            {
                if (GuidedDirectLocationCycles.Count > 0) return;

                var locationsCycles =
                    await LocationsCyclesCollection
                        .Find(Builders<LocationsCycle>.Filter.Empty)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);
                GuidedDirectLocationCycles.Clear();
                GuidedDirectLocationCycles.AddRange(
                    await locationsCycles
                        .OfType<GuidedDirectPeripheralLocationsCycle>()
                        .OrderBy(x => x.NumberOfLocations)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task Refresh()
        {
            _progress = true;
            OnStateChangeDelegate?.Invoke();

            await _travelingSalesManFoundationService.Refresh().ConfigureAwait(true);

            await Task.WhenAll(new[]
            {
                UpdateState(_travelingSalesManFoundationService.SliderValue, _cts.Token),
                Delay()
            })
            .ConfigureAwait(true);

            _progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        private Action PostInitCompleteHandler() => async () => await PostInitComplete();

        private async Task PostInitComplete()
        {
            await UpdateState(_travelingSalesManFoundationService.SliderValue, _cts.Token).ConfigureAwait(true);

            _isInit = true;
            _progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public async Task Init(TravelingSalesManAlgorithms algo)
        {
            _isInit = false;
            _progress = true;
            OnStateChangeDelegate?.Invoke();

            await _travelingSalesManFoundationService.Init(algo).ConfigureAwait(true);

            if (_travelingSalesManFoundationService.IsInit)
                await PostInitComplete().ConfigureAwait(true);
        }

        public Task SetExhaustiveItem(ExhaustiveIteration item) => SetExhaustiveItem(item, false, _cts.Token);

        private async Task SetExhaustiveItem(ExhaustiveIteration item, bool silent, CancellationToken cancellationToken)
        {
            try
            {
                if (ExhaustiveItem is not { }) return;

                if (!silent)
                {
                    _progress = true;
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
                    var cyclePairs = await item.Collection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines(cancellationToken).ConfigureAwait(true);
                    var matrix = await ExhaustiveItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                    ExhaustiveItem.Matrix.Clear();
                    ExhaustiveItem.MapChartData.Clear();

                    ExhaustiveItem.Matrix.AddRange(matrix);
                    ExhaustiveItem.MapChartData.AddRange(mapLineData.Concat(ExhaustiveItem.MapMarkerData));
                    ExhaustiveItem.SelectedIteration = item;

                    await _travelingSalesManHistoryService.SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Exhaustive,
                            item.Text,
                            item.DistanceInKilometers,
                            new(ExhaustiveItem.MapChartData)
                        ),
                        _travelingSalesManFoundationService.SliderValue,
                        cancellationToken
                    ).ConfigureAwait(true);
                }

                if (!silent)
                {
                    _progress = false;
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

                _progress = true;
                OnStateChangeDelegate?.Invoke();

                PartialRandomItem.Matrix.Clear();
                PartialRandomItem.MapChartData.Clear();

                PartialRandomItem.Matrix.AddRange(PartialRandomItem.ResetMatrix);
                PartialRandomItem.MapChartData.AddRange(PartialRandomItem.MapMarkerData);
                PartialRandomItem.SelectedIteration = default;
                PartialRandomItem.Builder = default;

                if (_travelingSalesManFoundationService.LocationsBySelection.Count > 0)
                    await SetPartialRandomLocation(_travelingSalesManFoundationService.LocationsBySelection[0], true, cancellationToken).ConfigureAwait(true);

                _progress = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task SetPartialRandomLocation(Guid locationId)
        {
            if (_travelingSalesManFoundationService.LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location)
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
                    _progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                if (PartialRandomItem.Builder is not { }) PartialRandomItem.Builder = new();

                PartialRandomItem.Builder.Collection[item.Id] = item with { };

                var collection = await PartialRandomItem.Builder.Collection.Values.ToListAsync(cancellationToken).ConfigureAwait(true);

                if (PartialRandomItem.Builder.Collection.Count == _travelingSalesManFoundationService.LocationsBySelection.Count)
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

                    await _travelingSalesManHistoryService.SetHistory(
                         new(
                             TravelingSalesManAlgorithms.Partial_Random,
                             text,
                             totalDistance,
                             await collection
                                .ToMapLines(PartialRandomItem.MapMarkerData, cancellationToken)
                                .ConfigureAwait(true)
                         ),
                         _travelingSalesManFoundationService.SliderValue,
                         cancellationToken
                     )
                     .ConfigureAwait(true);

                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();

                    PartialRandomItem.Matrix.AddRange(PartialRandomItem.ResetMatrix);
                    PartialRandomItem.MapChartData.AddRange(PartialRandomItem.MapMarkerData);

                    var locations = _travelingSalesManFoundationService.LocationsBySelection.Take(1).ToList();

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
                    var cyclePairs = await collection.ToPartialCyclePairs(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines(cancellationToken).ConfigureAwait(true);
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

                    if (PartialRandomItem.Builder.Collection.Count == _travelingSalesManFoundationService.LocationsBySelection.Count - 1)
                    {
                        var autoItem = _travelingSalesManFoundationService.LocationsBySelection.ExceptBy(collection.Select(x => x.Id), x => x.Id).First();
                        await SetPartialRandomLocation(autoItem, true, cancellationToken).ConfigureAwait(true);
                    }
                }

                if (!silent)
                {
                    _progress = false;
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
                    _progress = true;
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
                    var cyclePairs = await item.Collection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
                    var mapLineData = await cyclePairs.ToMapLines(cancellationToken).ConfigureAwait(true);
                    var matrix = await PartialRandomItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs, cancellationToken).ConfigureAwait(true);

                    PartialRandomItem.Matrix.Clear();
                    PartialRandomItem.MapChartData.Clear();

                    PartialRandomItem.Matrix.AddRange(matrix);
                    PartialRandomItem.MapChartData.AddRange(mapLineData.Concat(PartialRandomItem.MapMarkerData));
                    PartialRandomItem.SelectedIteration = item;
                }

                if (!silent)
                {
                    _progress = false;
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

            _progress = true;

            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdatePartialImprovingState(
                        _travelingSalesManFoundationService.LocationsBySelection,
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
                    _progress = false;
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

                    await _travelingSalesManHistoryService.SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Partial_Improving,
                            iteration.Text,
                            iteration.DistanceInKilometers,
                            PartialImprovingItem.MapChartData
                        ),
                        _travelingSalesManFoundationService.SliderValue,
                        _cts.Token
                    )
                    .ConfigureAwait(true);
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

            _progress = true;

            OnStateChangeDelegate?.Invoke();

            await Task.WhenAll(new[]
                {
                    UpdateGuidedDirectState(
                        _travelingSalesManFoundationService.LocationsBySelection,
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
                    _progress = false;
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
            if (_travelingSalesManFoundationService.LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location)
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
                    var autoItem = _travelingSalesManFoundationService.LocationsBySelection.ExceptBy(htcc.Visited.Values.Select(x => x.Node.Id), x => x.Id).First();
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
                        _progress = true;
                        OnStateChangeDelegate?.Invoke();

                        await Delay(2500).ConfigureAwait(true);

                        gdi.Rule = 2;
                        gdi.AllowRuleToggle = true;
                        _progress = false;
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
                    var autoItem = _travelingSalesManFoundationService.LocationsBySelection.ExceptBy(p.Visited.Values.Select(x => x.Node.Id), x => x.Id).First();
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

            _progress = true;
            OnStateChangeDelegate?.Invoke();

            UpdateEvolutionaryState(
                _travelingSalesManFoundationService.LocationsBySelection,
                EvolutionaryItem.NumberOfUniqueRoutes,
                EvolutionaryItem.ResetMatrix,
                EvolutionaryItem.MapMarkerData,
                TravelingSalesManAlgorithms.Evolutionary
            );

            await Delay().ConfigureAwait(true);

            _progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public Task SetEvolutionarySpin() => SetEvolutionarySpin(false);

        private async Task SetEvolutionarySpin(bool silent)
        {
            try
            {
                if (EvolutionaryItem is not { Step: 4, WheelItems.Count: > 0 }) return;

                if (!silent)
                {
                    EvolutionaryItem.Spinning = true;
                    _progress = true;
                    OnStateChangeDelegate?.Invoke();

                    await Delay(1000).ConfigureAwait(true);
                }

                var items =
                    EvolutionaryItem.WheelItems
                        .OrderBy(_ => Random.Shared.Next())
                        .Take(EvolutionaryItem.WheelItems.Count <= 2 ? 2 : 1)
                        .ToList();
                EvolutionaryItem.MatingPool.AddRange(items);
                EvolutionaryItem.CurrentGeneration.RemoveAll(x => items.Any(y => x.Id == y.Id));
                EvolutionaryItem.WheelItems.RemoveAll(x => items.Any(y => x.Id == y.Id));

                if (!silent)
                {
                    _progress = false;
                    EvolutionaryItem.Spinning = false;
                    OnStateChangeDelegate?.Invoke();
                }

                if (EvolutionaryItem.WheelItems.Count > 0) return;

                EvolutionaryItem.Offsprings.Clear();
                EvolutionaryItem.Offsprings.AddRange(
                    EvolutionaryItem.MatingPool
                        .ExceptBy(EvolutionaryItem.NextGeneration.Select(x => x.Text), x => x.Text)
                        .ComputeEvolutionaryOffsprings(EvolutionaryItem.NumberOfBitsOffspring - 1)
                        .DistinctBy(x => x.Text)
                        .ComputeEvolutionaryRanks()
                        .OrderBy(x => x.Rank)
                );

                await SetEvolutionaryStep(EvolutionaryItem.Step + 1).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task SetEvolutionaryStep(int step)
        {
            try
            {
                if (EvolutionaryItem is not { } || EvolutionaryItem.Step >= step) return;

                _progress = true;
                OnStateChangeDelegate?.Invoke();

                await Delay().ConfigureAwait(true);

                EvolutionaryItem.Step = step;

                switch (EvolutionaryItem.Step)
                {
                    case 1:
                        var first = EvolutionaryItem.CurrentGeneration[0] with { };
                        var randomizedLocations = new List<EvolutionaryNodes>();

                        for (int i = 0; i < 30; i++)
                        {
                            var nodes =
                                await first.Nodes
                                    .Skip(1)
                                    .OrderBy(x => Random.Shared.Next())
                                    .Prepend(first.Nodes[0])
                                    .ToListAsync(_cts.Token)
                                    .ConfigureAwait(true);
                            var nodesText = nodes.ToText();

                            if (first.Text == nodesText) continue;

                            var distanceInKilometers =
                                await nodes.ConvertAll(x => x.Location)
                                    .CalculateDistanceOfCycle(_cts.Token)
                                    .ConfigureAwait(true);

                            randomizedLocations.Add(
                                new()
                                {
                                    Id = Guid.NewGuid(),
                                    Nodes = nodes,
                                    Text = nodesText,
                                    DistanceInKilometers = distanceInKilometers
                                }
                            );
                        }

                        EvolutionaryItem.CurrentGeneration.Clear();
                        var generation =
                            randomizedLocations
                                .DistinctBy(x => x.Text)
                                .OrderBy(x => Random.Shared.Next())
                                .Take(9)
                                .Prepend(first)
                                .ComputeEvolutionaryRanks()
                                .OrderBy(x => x.Rank)
                                .ToList();
                        generation.Last().Rank = 1;
                        generation[0].Rank = 0;
                        EvolutionaryItem.CurrentGeneration.AddRange(generation);

                        EvolutionaryItem.Offsprings.AddRange(
                             EvolutionaryItem.CurrentGeneration
                                .Take(2)
                                .ComputeEvolutionaryOffsprings(EvolutionaryItem.NumberOfBitsOffspring)
                        );
                        break;
                    case 3:
                        EvolutionaryItem.NextGeneration.AddRange(EvolutionaryItem.CurrentGeneration.Where(x => x.Rank == 0));
                        break;
                    case 4:
                        EvolutionaryItem.WheelItems.AddRange(EvolutionaryItem.CurrentGeneration.Where(x => x.Rank > 0).Take(6));

                        if (EvolutionaryItem.WheelItems.Count == 0)
                            EvolutionaryItem.Step = 10;
                        else if (EvolutionaryItem.WheelItems.Count < 2)
                            await SetEvolutionarySpin(true).ConfigureAwait(true);
                        break;
                    case 7:
                        var nextGeneration =
                            EvolutionaryItem.NextGeneration
                                .Concat(EvolutionaryItem.Offsprings.OrderBy(x => x.Rank))
                                .ToList();
                        EvolutionaryItem.NextGeneration.Clear();
                        EvolutionaryItem.NextGeneration.AddRange(nextGeneration);
                        break;
                    case 9:
                        EvolutionaryItem.CurrentGenerationIteration++;
                        EvolutionaryItem.CurrentGeneration.Clear();
                        EvolutionaryItem.CurrentGeneration.AddRange(EvolutionaryItem.NextGeneration);
                        EvolutionaryItem.MatingPool.Clear();
                        EvolutionaryItem.WheelItems.Clear();
                        EvolutionaryItem.Offsprings.Clear();
                        EvolutionaryItem.NextGeneration.Clear();
                        EvolutionaryItem.Step = 2;
                        break;
                }

                _progress = false;
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
                    _progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                EvolutionaryItem.CurrentGeneration[0].Nodes.Add(item with { });
                EvolutionaryItem.Visited[item.Location.Id] = item with { };

                if (
                    EvolutionaryItem.Visited.Count == _travelingSalesManFoundationService.LocationsBySelection.Count - 1 &&
                    EvolutionaryItem.Nodes.ExceptBy(EvolutionaryItem.Visited.Keys, x => x.Location.Id).FirstOrDefault() is { } last
                )
                {
                    EvolutionaryItem.CurrentGeneration[0].Nodes.Add(last with { });
                    EvolutionaryItem.Visited[last.Location.Id] = last with { };
                }

                var collection =
                    await EvolutionaryItem.Visited.Values
                        .Select(x => x.Location)
                        .ToListAsync(cancellationToken).ConfigureAwait(true);
                var cycleComplete = EvolutionaryItem.Visited.Count == _travelingSalesManFoundationService.LocationsBySelection.Count;
                var cyclePairs =
                    await (
                        cycleComplete
                            ? collection.ToCyclePairs()
                            : collection.ToPartialCyclePairs()
                    )
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(true);
                var mapLineData = await cyclePairs.ToMapLines(cancellationToken).ConfigureAwait(true);
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
                    EvolutionaryItem.CurrentGeneration[0].DistanceInKilometers = totalDistance;
                    EvolutionaryItem.CurrentGeneration[0].Text = EvolutionaryItem.CurrentGeneration[0].ToText();
                    EvolutionaryItem.CycleComplete = true;
                }

                if (!silent)
                {
                    _progress = false;
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

        private async Task UpdateState(int sliderValue, CancellationToken cancellationToken)
        {
            _travelingSalesManFoundationService.SliderValue = sliderValue;

            try
            {
                #region Calculate
                var matrix = await _travelingSalesManFoundationService.LocationsBySelection.ToList().CalculateMatrix(cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutesPerNumberOfLocations = await sliderValue.CalculateNumberOfUniqueRoutesPerNumberOfLocations(cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutes = numberOfUniqueRoutesPerNumberOfLocations.LastOrDefault();
                var mapMarkerData = await CalculateMapMarkers(_travelingSalesManFoundationService.LocationsBySelection, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                #endregion

                #region Set
                UpdateNoneState(_travelingSalesManFoundationService.LocationsBySelection, matrix, numberOfUniqueRoutes, numberOfUniqueRoutesPerNumberOfLocations, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                await UpdatePreselectedState(_travelingSalesManFoundationService.LocationsBySelection, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                await UpdateExhaustiveState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                await UpdatePartialRandomState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                await UpdatePartialImprovingState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                await UpdateGuidedDirectState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                UpdateEvolutionaryState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdateNoneState(
            IReadOnlyList<LocationGeo> locations,
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

                if (locations.HasInsufficientData()) return;

                NoneItem.NumberOfUniqueRoutesPerNumberOfLocations.AddRange(numberOfUniqueRoutesPerNumberOfLocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task UpdatePreselectedState(
            IReadOnlyList<LocationGeo> locations,
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
                if (locations.HasInsufficientData())
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
                var totalDistance = await locations.ToList().CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                var mapLineData = await locations.ToList().ToMapLines(cancellationToken).ConfigureAwait(true);

                PreselectedItem = new()
                {
                    NumberOfUniqueRoutes = 1,
                    Text = locations.ToList().ToText(),
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
            IReadOnlyList<LocationGeo> locations,
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
                var maxExhaustiveLocationsToCalculateReached = locations.Count > _travelingSalesManFoundationService.MaxExhaustiveLocationsToCalculate;

                if (locations.HasInsufficientData() || maxExhaustiveLocationsToCalculateReached)
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

                var iterations = await locations.ToList().CalculateExhaustiveIterations(cancellationToken).ConfigureAwait(true);

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
            IReadOnlyList<LocationGeo> locations,
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

                if (locations.HasInsufficientData())
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
            IReadOnlyList<LocationGeo> locations,
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

                if (locations.HasInsufficientData())
                {
                    PartialImprovingItem.Matrix.AddRange(matrix);
                    PartialImprovingItem.MapChartData.AddRange(mapMarkerData);

                    return;
                }

                PartialImprovingItem.ResetMatrix.AddRange(matrix);
                PartialImprovingItem.MapMarkerData.AddRange(mapMarkerData);

                var iterations =
                    await locations
                        .ToList()
                        .ComputePartialImprovingIterations(matrix, _travelingSalesManFoundationService.MapsOptions.PartialImproving, cancellationToken)
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
            IReadOnlyList<LocationGeo> locations,
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

                if (locations.HasInsufficientData()) return;

                await SetLocationsCycleDataFromDatabase(cancellationToken).ConfigureAwait(true);

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
                            _travelingSalesManFoundationService.LocationsBySelection,
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
            IReadOnlyList<LocationGeo> locations,
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

                if (locations.HasInsufficientData()) return;

                EvolutionaryItem.NumberOfBitsOffspring = _travelingSalesManFoundationService.LocationsBySelection.Count switch
                {
                    < 5 => 2,
                    < 7 => 3,
                    _ => 4
                };
                EvolutionaryItem.Nodes.AddRange(locations.Select((x, i) => new EvolutionaryNode(i, x)));
                var first = EvolutionaryItem.Nodes[0];
                EvolutionaryItem.CurrentGeneration.Add(new()
                {
                    Id = Guid.NewGuid(),
                    Text = "...",
                    Nodes = new() { first with { } }
                });
                EvolutionaryItem.Visited[first.Location.Id] = first with { };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<List<ITrace>> CalculateMapMarkers(IReadOnlyList<LocationGeo> locations, TravelingSalesManAlgorithms algo, CancellationToken cancellationToken)
        {
            try
            {
                if (locations.Count == 0)
                    return new() { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };

                return await (algo switch
                {
                    TravelingSalesManAlgorithms.Preselected => locations.ToPreselectedMarkers(),
                    TravelingSalesManAlgorithms.Evolutionary => locations.ToEvolutionaryMarkers(_travelingSalesManFoundationService.MapsOptions.Evolutionary),
                    _ => locations.ToDefaultMarkers()
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new() { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } };
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;

            _cts.Cancel();

            _travelingSalesManFoundationService.OnInitComplete -= PostInitCompleteHandler();
            _travelingSalesManFoundationService.OnProgress -= OnProgressHandler();
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
