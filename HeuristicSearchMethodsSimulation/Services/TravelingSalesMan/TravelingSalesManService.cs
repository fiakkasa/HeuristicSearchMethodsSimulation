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

        public bool EnableBuilders => _travelingSalesManFoundationService.EnableBuilders;
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
        }

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
                    locationsCycles
                        .OfType<GuidedDirectPeripheralLocationsCycle>()
                        .OrderBy(x => x.NumberOfLocations)
                        .ToList()
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

        public async Task Init(TravelingSalesManAlgorithms algo)
        {
            _travelingSalesManFoundationService.Algorithm = algo;
            _progress = true;
            OnStateChangeDelegate?.Invoke();

            await _travelingSalesManFoundationService.Init(algo).ConfigureAwait(true);

            while (!_travelingSalesManFoundationService.IsInit)
                await Delay().ConfigureAwait(true);

            await UpdateState(_travelingSalesManFoundationService.SliderValue, _cts.Token).ConfigureAwait(true);

            _isInit = true;
            _progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public void SetExhaustiveItem(ExhaustiveIteration item) => SetExhaustiveItem(item, false);

        private void SetExhaustiveItem(ExhaustiveIteration item, bool silent)
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
                    var cyclePairs = item.Collection.ToCyclePairs();
                    var mapLineData = cyclePairs.ToMapLines();
                    var matrix = ExhaustiveItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs);

                    ExhaustiveItem.Matrix.Clear();
                    ExhaustiveItem.MapChartData.Clear();

                    ExhaustiveItem.Matrix.AddRange(matrix);
                    ExhaustiveItem.MapChartData.AddRange(mapLineData.Concat(ExhaustiveItem.MapMarkerData));
                    ExhaustiveItem.SelectedIteration = item;

                    _travelingSalesManHistoryService.SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Exhaustive,
                            item.Text,
                            item.DistanceInKilometers,
                            new(ExhaustiveItem.MapChartData)
                        ),
                        _travelingSalesManFoundationService.SliderValue
                    );
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

        public void ClearPartialRandomBuilder()
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
                    SetPartialRandomLocation(_travelingSalesManFoundationService.LocationsBySelection[0], true);

                _progress = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public void SetPartialRandomLocation(Guid locationId)
        {
            if (
                PartialRandomItem?.Builder?.Collection.ContainsKey(locationId) != true
                && _travelingSalesManFoundationService.LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location
            )
            {
                SetPartialRandomLocation(location, true);
            }
        }

        public void SetPartialRandomLocation(LocationGeo item) => SetPartialRandomLocation(item, false);

        private void SetPartialRandomLocation(LocationGeo item, bool silent)
        {
            try
            {
                if (!(PartialRandomItem is { } && PartialRandomItem.Builder?.Collection.ContainsKey(item.Id) != true)) return;

                if (!silent)
                {
                    _progress = true;
                    OnStateChangeDelegate?.Invoke();
                }

                if (PartialRandomItem.Builder is not { }) PartialRandomItem.Builder = new();

                PartialRandomItem.Builder.Collection[item.Id] = item with { };

                var collection = PartialRandomItem.Builder.Collection.Values.ToList();

                if (PartialRandomItem.Builder.Collection.Count == _travelingSalesManFoundationService.LocationsBySelection.Count)
                {
                    var totalDistance = collection.CalculateDistanceOfCycle();
                    var text = collection.ToText();

                    var obj = new PartialRandomIteration(
                        collection,
                        text,
                        totalDistance,
                        Guid.NewGuid()
                    );

                    PartialRandomItem.Iterations.Add(obj);

                    _travelingSalesManHistoryService.SetHistory(
                         new(
                             TravelingSalesManAlgorithms.Partial_Random,
                             text,
                             totalDistance,
                             collection.ToMapLines(PartialRandomItem.MapMarkerData)
                         ),
                         _travelingSalesManFoundationService.SliderValue
                     );

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
                    var cyclePairs = collection.ToPartialCyclePairs();
                    var mapLineData = cyclePairs.ToMapLines();
                    var totalDistance = cyclePairs.CalculateDistanceOfCycle();
                    var text = collection.ToText(customLastElemText: "...");
                    var matrix = PartialRandomItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs);

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
                        SetPartialRandomLocation(autoItem, true);
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

        public void SetPartialRandomItem(PartialRandomIteration item) => SetPartialRandomIteration(item, false);

        private void SetPartialRandomIteration(PartialRandomIteration item, bool silent)
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
                    var cyclePairs = item.Collection.ToCyclePairs();
                    var mapLineData = cyclePairs.ToMapLines();
                    var matrix = PartialRandomItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs);

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

        public void ResetPartialImproving()
        {
            if (PartialImprovingItem is not { }) return;

            _progress = true;

            OnStateChangeDelegate?.Invoke();

            UpdatePartialImprovingState(
                _travelingSalesManFoundationService.LocationsBySelection,
                PartialImprovingItem.NumberOfUniqueRoutes,
                PartialImprovingItem.ResetMatrix,
                PartialImprovingItem.MapMarkerData,
                TravelingSalesManAlgorithms.Partial_Improving
            );
            _progress = false;
            OnStateChangeDelegate?.Invoke();
        }

        public void PartialImprovingNextIteration()
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

                    _travelingSalesManHistoryService.SetHistory(
                        new(
                            TravelingSalesManAlgorithms.Partial_Improving,
                            iteration.Text,
                            iteration.DistanceInKilometers,
                            PartialImprovingItem.MapChartData
                        ),
                        _travelingSalesManFoundationService.SliderValue
                    );
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
            if (
                GuidedDirectItem?.Visited.ContainsKey(locationId) != true
                && _travelingSalesManFoundationService.LocationsBySelection.Skip(1).FirstOrDefault(x => x.Id == locationId) is { } location
            )
            {
                await SetGuidedDirectSelection(location).ConfigureAwait(true);
            }
        }

        public async Task SetGuidedDirectSelection(LocationGeo location)
        {
            try
            {
                if (!(GuidedDirectItem is { } gdi && !GuidedDirectItem.Visited.ContainsKey(location.Id))) return;

                if (gdi.Rule == 1)
                    await HeadToClosestCityRule(gdi).ConfigureAwait(true);
                else if (gdi.Rule == 2)
                    await PeripheralRule(gdi).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            async Task HeadToClosestCityRule(GuidedDirectItem gdi)
            {
                if (
                    gdi.HeadToClosestCity is not { Iterations.Count: > 0, Current: { }, Next: { } } htcc
                    || htcc.Index >= htcc.Iterations.Count
                    || !htcc.Iterations.Any(x => x.Node.Id == location.Id)
                )
                {
                    return;
                }

                if (location.Id != htcc.Next.Node.Id)
                {
                    if (!htcc.Log.Any(x => x.StartsWith(location.ShortCode)))
                    {
                        htcc.Log.Add($"{location.ShortCode} is not the best choice, please refer to the rule and try again.");
                        OnStateChangeDelegate?.Invoke();
                    }

                    return;
                }

                htcc.Visited[htcc.Next.Node.Id] = htcc.Next with { };
                htcc.Index++;

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

                    if (gdi.Peripheral is { Solutions.Count: > 0 } p)
                    {
                        _progress = true;
                        OnStateChangeDelegate?.Invoke();

                        await Delay(2500).ConfigureAwait(true);

                        gdi.Rule = 2;
                        gdi.AllowRuleToggle = true;
                        _progress = false;
                    }
                }

                OnStateChangeDelegate?.Invoke();
            }

            async Task PeripheralRule(GuidedDirectItem gdi)
            {
                if (
                    gdi.Peripheral is not { Solutions.Count: > 0 } p
                    || (p.Index > 0 && p.Index >= p.Iterations.Count)
                )
                {
                    return;
                }

                if (p.Index == 0 && p.Solutions.FirstOrDefault(x => x.Skip(1).FirstOrDefault()?.Id == location.Id) is { } solution)
                {
                    p.Solution.AddRange(solution);
                    p.Iterations.AddRange(
                        p.Solution.ComputeGuidedDirectIterationsFromGuidedDirectCollection(p.Matrix, p.MapMarkerData)
                    );
                }

                if (p is not { Iterations.Count: > 0, Current: { } }) return;

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

        public void ResetEvolutionary()
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
                                first.Nodes
                                    .Skip(1)
                                    .OrderBy(x => Random.Shared.Next())
                                    .Prepend(first.Nodes[0])
                                    .ToList();
                            var nodesText = nodes.ToText();

                            if (first.Text == nodesText) continue;

                            var distanceInKilometers = nodes.CalculateDistanceOfCycle();

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

        public void SetEvolutionaryLocation(EvolutionaryNode item) => SetEvolutionaryLocation(item, false);

        public void SetEvolutionaryLocation(Guid locationId)
        {
            if (
                EvolutionaryItem?.Visited.ContainsKey(locationId) != true
                && EvolutionaryItem?.Nodes.Skip(1).FirstOrDefault(x => x.Location.Id == locationId) is { } node
            )
            {
                SetEvolutionaryLocation(node, false);
            }
        }

        private void SetEvolutionaryLocation(EvolutionaryNode item, bool silent)
        {
            try
            {
                if (!(EvolutionaryItem is { CycleComplete: false } && !EvolutionaryItem.Visited.ContainsKey(item.Location.Id))) return;

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
                    EvolutionaryItem.Visited.Values
                        .Select(x => x.Location)
                        .ToList();
                var cycleComplete = EvolutionaryItem.Visited.Count == _travelingSalesManFoundationService.LocationsBySelection.Count;
                var cyclePairs = (cycleComplete ? collection.ToCyclePairs() : collection.ToPartialCyclePairs()).ToList();
                var mapLineData = cyclePairs.ToMapLines();
                var totalDistance = cyclePairs.CalculateDistanceOfCycle();
                var matrix = EvolutionaryItem.ResetMatrix.HighlightMatrixCyclePairs(cyclePairs);

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
                var (matrix, numberOfUniqueRoutesPerNumberOfLocations, numberOfUniqueRoutes, mapMarkerData) = await BaseData().ConfigureAwait(true);
                #endregion

                #region Set
                UpdateNoneState(_travelingSalesManFoundationService.LocationsBySelection, matrix, numberOfUniqueRoutes, numberOfUniqueRoutesPerNumberOfLocations, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                UpdatePreselectedState(_travelingSalesManFoundationService.LocationsBySelection, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                await UpdateExhaustiveState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                UpdatePartialRandomState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                UpdatePartialImprovingState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                await UpdateGuidedDirectState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm, cancellationToken).ConfigureAwait(true);
                UpdateEvolutionaryState(_travelingSalesManFoundationService.LocationsBySelection, numberOfUniqueRoutes, matrix, mapMarkerData, _travelingSalesManFoundationService.Algorithm);
                #endregion
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            async Task<(List<LocationRow> matrix, List<long> numberOfUniqueRoutesPerNumberOfLocations, long numberOfUniqueRoutes, List<ITrace> mapMarkerData)> BaseData()
            {
                var matrix = await Task.Run(() => _travelingSalesManFoundationService.LocationsBySelection.CalculateMatrix(), cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutesPerNumberOfLocations = await Task.Run(() => sliderValue.CalculateNumberOfUniqueRoutesPerNumberOfLocations(), cancellationToken).ConfigureAwait(true);
                var numberOfUniqueRoutes = await Task.Run(() => numberOfUniqueRoutesPerNumberOfLocations.LastOrDefault(), cancellationToken).ConfigureAwait(true);
                var mapMarkerData = await Task.Run(() => CalculateMapMarkers(_travelingSalesManFoundationService.LocationsBySelection, _travelingSalesManFoundationService.Algorithm), cancellationToken).ConfigureAwait(true);

                return (matrix, numberOfUniqueRoutesPerNumberOfLocations, numberOfUniqueRoutes, mapMarkerData);
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

                if (locations.HasInsufficientData()) return;

                NoneItem.NumberOfUniqueRoutesPerNumberOfLocations.AddRange(numberOfUniqueRoutesPerNumberOfLocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdatePreselectedState(
            List<LocationGeo> locations,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo
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
                        matrix
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
                            .ToList(),
                    _ => matrix
                };
                var totalDistance = locations.CalculateDistanceOfCycle();
                var mapLineData = locations.ToMapLines();

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
                var maxExhaustiveLocationsToCalculateReached = locations.Count > _travelingSalesManFoundationService.MaxExhaustiveLocationsToCalculate;

                if (locations.HasInsufficientData() || maxExhaustiveLocationsToCalculateReached)
                {
                    ExhaustiveItem = new()
                    {
                        NumberOfUniqueRoutes = numberOfUniqueRoutes,
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
                    SetExhaustiveItem(iterations[0], true);
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

        private void UpdatePartialRandomState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo
        )
        {
            PartialRandomItem = default;

            if (algo != TravelingSalesManAlgorithms.Partial_Random) return;

            try
            {
                PartialRandomItem = new() { NumberOfUniqueRoutes = numberOfUniqueRoutes };

                PartialRandomItem.Matrix.AddRange(matrix);
                PartialRandomItem.MapChartData.AddRange(mapMarkerData);
                PartialRandomItem.ResetMatrix.AddRange(matrix);
                PartialRandomItem.MapMarkerData.AddRange(mapMarkerData);

                if (locations.HasInsufficientData()) return;

                SetPartialRandomLocation(locations[0], true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void UpdatePartialImprovingState(
            List<LocationGeo> locations,
            long? numberOfUniqueRoutes,
            List<LocationRow> matrix,
            List<ITrace> mapMarkerData,
            TravelingSalesManAlgorithms algo
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
                    locations
                        .Skip(1)
                        .OrderBy(_ => Random.Shared.Next())
                        .Prepend(locations[0])
                        .ToList()
                        .ComputePartialImprovingIterations(matrix, _travelingSalesManFoundationService.MapsOptions.PartialImproving)
                        .ToList();

                var computed = iterations[0] with { };

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

                if (locations.HasInsufficientData()) return;

                await SetLocationsCycleDataFromDatabase(cancellationToken).ConfigureAwait(true);

                GuidedDirectItem.HeadToClosestCity.Log.Add("RULE 1: Always head for the closest city.");
                GuidedDirectItem.Peripheral.Log.Add("RULE 2: Head for the closest city, while sticking to an exterior route.");

                var headToClosestCityCollection =
                    matrix
                        .ComputeHeadToClosestCityGuidedDirectCollection()
                        .ToList();
                var headToClosestCityIterations =
                    headToClosestCityCollection
                        .ComputeGuidedDirectIterationsFromGuidedDirectCollection(matrix, mapMarkerData)
                        .ToList();

                GuidedDirectItem.HeadToClosestCity.Solution = headToClosestCityCollection;
                GuidedDirectItem.HeadToClosestCity.Visited.Add(headToClosestCityIterations[0].Node.Id, headToClosestCityIterations[0]);
                GuidedDirectItem.HeadToClosestCity.Iterations.AddRange(headToClosestCityIterations);

                var firstItem = locations[0] with { };
                var peripheralCyclesForNumberOfCities =
                    GuidedDirectLocationCycles
                        .Where(x =>
                            x.Collection.FirstOrDefault() == firstItem.Id
                            && x.NumberOfLocations == _travelingSalesManFoundationService.LocationsBySelection.Count
                        )
                        .ToList();

                if (peripheralCyclesForNumberOfCities is not { Count: > 0 }) return;

                var peripheralCollections =
                    peripheralCyclesForNumberOfCities
                        .ConvertAll(sol =>
                            sol.Collection.Join(
                                _travelingSalesManFoundationService.LocationsBySelection,
                                s => s,
                                l => l.Id,
                                (_, l) => l
                            )
                            .ToList()
                        );

                var visited = peripheralCollections[0].ComputeGuidedDirectIterationsFromGuidedDirectCollection(matrix, mapMarkerData).First();

                GuidedDirectItem.Peripheral.Solutions = peripheralCollections;
                GuidedDirectItem.Peripheral.Visited.Add(visited.Node.Id, visited with { });
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

        private List<ITrace> CalculateMapMarkers(IEnumerable<LocationGeo> locations, TravelingSalesManAlgorithms algo) =>
            !locations.Any()
                ? new() { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } }
                : algo switch
                {
                    TravelingSalesManAlgorithms.Preselected => locations.ToPreselectedMarkers(),
                    TravelingSalesManAlgorithms.Evolutionary => locations.ToEvolutionaryMarkers(_travelingSalesManFoundationService.MapsOptions.Evolutionary),
                    _ => locations.ToDefaultMarkers()
                };

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
