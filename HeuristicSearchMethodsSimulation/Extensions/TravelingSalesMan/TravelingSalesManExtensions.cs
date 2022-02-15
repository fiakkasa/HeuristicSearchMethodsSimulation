using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManExtensions
    {
        public static async Task<List<LocationRow>> CalculateMatrix(this List<LocationGeo> locations, CancellationToken cancellationToken = default) =>
            await locations
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
                        YId: location.Id,
                        Xlabels: locations.ConvertAll(x => $"{x.Label} ({x.ShortCode})")
                    );
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);

        public static async Task<List<LocationRow>> ResetMatrix(this List<LocationRow> matrix, CancellationToken cancellationToken = default) =>
            await matrix
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

        public static async Task<List<LocationRow>> HighlightMatrixCyclePairs(this List<LocationRow> matrix, List<LocationPair> cyclePairs, CancellationToken cancellationToken = default) =>
            await matrix
                .Select((row, rowIndex) => row with
                {
                    Collection =
                        row.Collection
                            .Select((cell, cellIndex) => cell with
                            {
                                IsHighlightedDistance = cyclePairs.Any(pair => cell.A.Id != cell.B.Id && cell.A.Id == pair.A.Id && cell.B.Id == pair.B.Id)
                            })
                            .ToList()
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);

        public static bool HasInsufficientData<T>(this List<T>? collection) =>
            (collection?.Count ?? 0) < Consts.MinNumberOfLocations;

        public static bool HasInsufficientData<T>(this IEnumerable<T>? collection) =>
           (collection?.Take(Consts.MinNumberOfLocations).Count() ?? 0) < Consts.MinNumberOfLocations;

        public static async Task<bool> HasInsufficientData<T>(this IAsyncEnumerable<T>? collection, CancellationToken cancellationToken) =>
            (collection is { } ? await collection.CountAsync(cancellationToken).ConfigureAwait(true) : 0) < Consts.MinNumberOfLocations;

        private static string GetDistanceFormat(bool simple = false) => simple switch
        {
            true => "#0",
            _ => "#0.## Km"
        };

        public static string? ToFormattedDistance(this double? value, bool simple = false) =>
            value?.ToString(GetDistanceFormat(simple));

        public static string ToFormattedDistance(this double value, bool simple = false) =>
            value.ToString(GetDistanceFormat(simple));

        public static string ToKey(this LocationGeo location, LocationGeo otherLocation) =>
            location.Id.CompareTo(otherLocation.Id) <= 0
                ? location.ToDirectionalKey(otherLocation)
                : location.ToReverseDirectionalKey(otherLocation);

        public static string ToDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{location.Id}-{otherLocation.Id}";

        public static string ToReverseDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{otherLocation.Id}-{location.Id}";

        public static string ToText(this List<LocationGeo> collection, string separator = " > ", string? customLastElemText = default) =>
            string.Join(
                separator,
                collection
                    .Select(x => x?.ShortCode)
                    .Append(customLastElemText?.Trim() is { Length: > 0 } ? customLastElemText : collection.FirstOrDefault()?.ShortCode)
                    .Where(x => x is not null)
                    .Select(x => x)
            );

        public static string ToText(this EvolutionaryNodes obj) => obj.Nodes.ToText();

        public static string ToText(this List<EvolutionaryNode> collection) =>
            string.Join("", collection.Skip(1).Select(y => y.Ordinal));

        public static IEnumerable<LocationPair> ToPartialCyclePairs(this List<LocationGeo> collection)
        {
            if (collection.Count >= 2)
            {
                for (int i = 1; i < collection.Count; i++)
                    yield return new(collection[i - 1], collection[i]);
            }
        }

        public static IEnumerable<LocationPair> ToCyclePairs(this List<LocationGeo> collection)
        {
            if (collection.Count >= Consts.MinNumberOfLocations)
            {
                for (int i = 1; i < collection.Count; i++)
                    yield return new(collection[i - 1], collection[i]);

                yield return new(collection[^1], collection[0]);
            }
        }

        public static double CalculateDistancePointToPointInKilometers(this LocationGeo location, LocationGeo otherLocation) =>
            (location, otherLocation) is { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } }
                ? lcGeo.GetDistanceTo(olcGeo) / 1000
                : 0D;

        public static async Task<double> CalculateDistanceOfCycle(this List<LocationPair> collection, CancellationToken cancellationToken) =>
            await collection
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .ToAsyncEnumerable()
                .SumAsync(cancellationToken)
                .ConfigureAwait(true);

        public static async Task<double> CalculateDistanceOfCycle(this List<LocationGeo> collection, CancellationToken cancellationToken) =>
            await collection
                .ToCyclePairs()
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .ToAsyncEnumerable()
                .SumAsync(cancellationToken)
                .ConfigureAwait(true);

        public static double CalculateDistanceOfCycle(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .Sum();

        public static ScatterGeo ToMapLine(this LocationPair x) =>
            new()
            {
                LocationMode = LocationModeEnum.ISO3,
                Lon = new List<object> { x.A.Longitude, x.B.Longitude },
                Lat = new List<object> { x.A.Latitude, x.B.Latitude },
                Mode = ModeFlag.Lines,
                Meta = (x.A.Id, x.B.Id)
            };

        public static IEnumerable<ITrace> ToMapLines(this IEnumerable<LocationPair> collection) =>
            collection.Select(x => x.ToMapLine());

        public static IEnumerable<ITrace> ToMapLines(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .ToMapLines();

        public static IEnumerable<List<LocationGeo>> CalculatePermutations(this List<LocationGeo> collection, Guid id)
        {
            return Permute(collection).Where(x => x.Count > 0 && x[0].Id == id);

            static IEnumerable<List<LocationGeo>> Permute(List<LocationGeo> set)
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
        }

        public static async IAsyncEnumerable<PartialImprovingIteration> ComputePartialImprovingIterations(
            this List<LocationGeo> startingCollection,
            List<LocationRow> matrix,
            PartialImprovingMapOptions mapOptions,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            var startingCycle = await startingCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
            var startingDistance = await startingCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
            var startingMapLinesData = await startingCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
            var startingMatrix = await matrix.HighlightMatrixCyclePairs(startingCycle, cancellationToken).ConfigureAwait(true);

            yield return new PartialImprovingIteration(
                startingCollection,
                startingCycle,
                startingMatrix,
                "Randomly traverse initial route.",
                startingCollection.ToText(),
                startingDistance,
                startingMapLinesData
            );

            if (startingCollection.HasInsufficientData()) yield break;

            var minDistance = startingDistance;
            var evaluatedCollection = startingCollection;
            var counter = 0L;
            const long maxIterations = 1_000;
            var controlQueue = new Queue<double>();

            do
            {
                for (var i = 1; i < evaluatedCollection.Count - 1; i++)
                {
                    for (var j = i + 1; j < evaluatedCollection.Count; j++)
                    {
                        var a = evaluatedCollection[i] with { };
                        var b = evaluatedCollection[j] with { };

                        var iterationCollection = new List<LocationGeo>(evaluatedCollection)
                        {
                            [i] = b,
                            [j] = a
                        };

                        var iterationDistance = await iterationCollection.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);

                        if (iterationDistance < minDistance)
                        {
                            var previousMatchCycle = await evaluatedCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationCycle = await iterationCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMapLinesData = await iterationCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMatrix = await matrix.HighlightMatrixCyclePairs(iterationCycle, cancellationToken).ConfigureAwait(true);
                            var iterationText = iterationCollection.ToText();

                            var pairsToKeep =
                                await iterationCycle
                                    .Select(x => x.A.ToKey(x.B))
                                    .ExceptBy(previousMatchCycle.Select(x => x.A.ToKey(x.B)), x => x)
                                    .ToListAsync(cancellationToken).ConfigureAwait(true);

                            var cyclesDiff =
                                await previousMatchCycle
                                    .Zip(iterationCycle)
                                    .Join(
                                        pairsToKeep,
                                        ecc => ecc.Second.A.ToKey(ecc.Second.B),
                                        ptk => ptk,
                                        (ecc, _) => ecc
                                    )
                                    .ToListAsync(cancellationToken).ConfigureAwait(true);

                            foreach (var (First, Second) in cyclesDiff)
                            {
                                var swapPairALine = First.ToMapLine();
                                swapPairALine.Line = new() { Color = mapOptions.SwapPairALineColor, Width = mapOptions.SwapPairALineWidth };

                                var swapPairBLine = Second.ToMapLine();
                                swapPairBLine.Line = new() { Color = mapOptions.SwapPairBLineColor, Width = mapOptions.SwapPairBLineWidth };

                                yield return new PartialImprovingIteration(
                                    iterationCollection,
                                    iterationCycle,
                                    iterationMatrix,
                                    $"Swap edge ({First.A.ShortCode}-{First.B.ShortCode}) with edge ({Second.A.ShortCode}-{Second.B.ShortCode})",
                                    iterationText,
                                    iterationDistance,
                                    iterationMapLinesData.Append(swapPairALine).Append(swapPairBLine).ToList()
                                );
                            }

                            minDistance = iterationDistance;
                            evaluatedCollection = iterationCollection;
                        }
                    }
                }

                controlQueue.Enqueue(minDistance);
                counter++;
            }
            while (
                !(
                    controlQueue.Count > 10
                    && controlQueue.Skip(1).Take(10).Distinct().Count() == 1
                )
                && counter <= maxIterations
            );

            var lastIterationCycle = await evaluatedCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);

            yield return new PartialImprovingIteration(
                evaluatedCollection,
                lastIterationCycle,
                await matrix.HighlightMatrixCyclePairs(lastIterationCycle, cancellationToken).ConfigureAwait(true),
                "No further improvement can be made via pairwise exchange.",
                evaluatedCollection.ToText(),
                await lastIterationCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true),
                await lastIterationCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true)
            );
        }

        public static async Task<(string Route, double DistanceInKilometers)> ComputePartialImprovingSolution(
            this List<LocationGeo> startingCollection,
            CancellationToken cancellationToken
        )
        {
            return await Compute(startingCollection, cancellationToken).LastOrDefaultAsync(cancellationToken).ConfigureAwait(true);

            static async IAsyncEnumerable<(string Route, double DistanceInKilometers)> Compute(
                List<LocationGeo> startingCollection,
                [EnumeratorCancellation] CancellationToken cancellationToken
            )
            {
                var startingCycle = await startingCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                var startingDistance = await startingCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);

                yield return (Route: startingCollection.ToText(), DistanceInKilometers: startingDistance);

                if (startingCollection.HasInsufficientData()) yield break;

                var minDistance = startingDistance;
                var evaluatedCollection = startingCollection;
                var counter = 0L;
                const long maxIterations = 1_000;
                var controlQueue = new Queue<double>();

                do
                {
                    for (var i = 1; i < evaluatedCollection.Count - 1; i++)
                    {
                        for (var j = i + 1; j < evaluatedCollection.Count; j++)
                        {
                            var a = evaluatedCollection[i] with { };
                            var b = evaluatedCollection[j] with { };

                            var iterationCollection = new List<LocationGeo>(evaluatedCollection)
                            {
                                [i] = b,
                                [j] = a
                            };

                            var iterationDistance = await iterationCollection.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);

                            if (iterationDistance < minDistance)
                            {
                                minDistance = iterationDistance;
                                evaluatedCollection = iterationCollection;

                                yield return (Route: evaluatedCollection.ToText(), DistanceInKilometers: iterationDistance);
                            }
                        }
                    }

                    controlQueue.Enqueue(minDistance);
                    counter++;
                }
                while (
                    !(
                        controlQueue.Count > 10
                        && controlQueue.Skip(1).Take(10).Distinct().Count() == 1
                    )
                    && counter <= maxIterations
                );
            }
        }

        public static async IAsyncEnumerable<GuidedDirectIteration> ComputeGuidedDirectIterationsFromGuidedDirectCollection(
            this List<LocationGeo> collection,
            List<LocationRow> matrix,
            List<ITrace> markers,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            if (collection.HasInsufficientData()) yield break;

            var iterationCollection = new List<LocationGeo>();

            for (int i = 0; i < collection.Count; i++)
            {
                var item = collection[i] with { };
                iterationCollection.Add(item);

                var iterationCyclePairs = await iterationCollection.ToPartialCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                var iterationMatrix = await matrix.HighlightMatrixCyclePairs(iterationCyclePairs, cancellationToken).ConfigureAwait(true);
                var iterationDistance = await iterationCyclePairs.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
                var iterationMapLineData = await iterationCyclePairs.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);

                yield return new GuidedDirectIteration(
                    i,
                    iterationCollection.ToText(customLastElemText: "..."),
                    iterationDistance,
                    collection[i] with { },
                    iterationCyclePairs,
                    iterationMatrix,
                    iterationMapLineData.Concat(markers).ToList(),
                    markers,
                    iterationMapLineData
                );
            }

            var computedCycle = await collection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
            var computedMatrix = await matrix.HighlightMatrixCyclePairs(computedCycle, cancellationToken).ConfigureAwait(true);
            var computedDistance = await computedCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
            var computedMapLinesData = await computedCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);

            yield return new GuidedDirectIteration(
                collection.Count,
                collection.ToText(),
                computedDistance,
                collection[0] with { },
                computedCycle,
                computedMatrix,
                computedMapLinesData.Concat(markers).ToList(),
                markers,
                computedMapLinesData
            );
        }

        public static IEnumerable<LocationGeo> ComputeHeadToClosestCityGuidedDirectCollection(this List<LocationRow> matrix)
        {
            if (matrix.FirstOrDefault()?.Collection.FirstOrDefault() is not { } start) yield break;

            yield return start.A;

            var currentId = start.A.Id;
            var visited = new Dictionary<Guid, Guid>
            {
                [currentId] = currentId
            };

            var matrixDict =
                matrix
                    .DistinctBy(x => x.YId)
                    .ToDictionary(k => k.YId, v => v.Collection);

            for (int i = 0; i < matrix.Count; i++)
            {
                if (!matrixDict.TryGetValue(currentId, out var collection)) continue;

                var smallestDistancePair =
                    collection
                        .Where(x => x.B.Id != currentId && !visited.ContainsKey(x.B.Id))
                        .MinBy(x => x.DistanceInKilometers);

                if (smallestDistancePair?.B is not { } found) continue;

                yield return found;

                currentId = found.Id;
                visited[currentId] = currentId;
            }
        }

        public static IEnumerable<EvolutionaryNodes> ComputeEvolutionaryOffsprings(this IEnumerable<EvolutionaryNodes> collection, int numberOfBits)
        {
            var i = 0;

            foreach (var nodes in collection.Select(x => x.Nodes))
            {
                var seed = collection
                    .Skip(i switch
                    {
                        0 => 1,
                        _ when i == nodes.Count - 1 => nodes.Count - 2,
                        _ => i - 1
                    })
                    .FirstOrDefault()?
                    .Nodes;
                if (seed is not { }) break;

                var tail = nodes.Skip(numberOfBits).ToList();
                var newTail = new List<EvolutionaryNode>();

                for (var j = 0; j < seed.Count; j++)
                {
                    if (!tail.Contains(seed[j])) continue;

                    newTail.Add(seed[j]);
                }

                var result =
                    nodes
                        .Take(numberOfBits)
                        .Concat(newTail)
                        .ToList();

                yield return new()
                {
                    Id = Guid.NewGuid(),
                    Nodes = result,
                    Text = result.ToText(),
                    DistanceInKilometers =
                        result
                            .ConvertAll(x => x.Location)
                            .CalculateDistanceOfCycle()
                };

                i++;
            }
        }

        public static IEnumerable<EvolutionaryNodes> ComputeEvolutionaryRanks(this IEnumerable<EvolutionaryNodes> collection)
        {
            if (!collection.Any())
                return Enumerable.Empty<EvolutionaryNodes>();

            var min = collection.Where(x => x.DistanceInKilometers > 0).MinBy(x => x.DistanceInKilometers)?.DistanceInKilometers;
            var max = collection.Where(x => x.DistanceInKilometers > 0).MaxBy(x => x.DistanceInKilometers)?.DistanceInKilometers;

            if ((min, max) is { min: not { } or <= 0, max: not { } or <= 0 }) return new List<EvolutionaryNodes>();

            var maxMin = max - min;

            return collection.Select(x => x with { Rank = (x.DistanceInKilometers - min) / maxMin });
        }
    }
}
