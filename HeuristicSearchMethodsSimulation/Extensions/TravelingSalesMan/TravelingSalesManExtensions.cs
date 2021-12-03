using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
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

        public static IEnumerable<List<LocationGeo>> Permute(this List<LocationGeo> set, Guid id) =>
            set.Permute().Where(x => x.Count > 0 && x[0].Id == id);

        public static IEnumerable<List<LocationGeo>> Permute(this List<LocationGeo> set)
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

        public static bool HasInsufficientLocations<T>(this List<T>? collection) where T : Location =>
            (collection?.Count ?? 0) < Consts.MinNumberOfLocations;

        public static string? ToFormattedDistance(this double? value) => value?.ToString("#0.## Km");

        public static string ToFormattedDistance(this double value) => value.ToString("#0.## Km");

        public static string ToKey(this LocationGeo location, LocationGeo otherLocation) =>
            location.ShortCode.CompareTo(otherLocation.ShortCode) <= 0
                ? location.ToDirectionalKey(otherLocation)
                : location.ToReverseDirectionalKey(otherLocation);

        public static string ToDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{location.ShortCode}-{otherLocation.ShortCode}";

        public static string ToReverseDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{otherLocation.ShortCode}-{location.ShortCode}";

        public static string ToText(this List<LocationGeo> collection, string separator = " > ", string? customLastElemText = default) =>
            string.Join(
                separator,
                collection
                    .Select(x => x?.ShortCode)
                    .Append(customLastElemText?.Trim() is { Length: > 0 } ? customLastElemText : collection.FirstOrDefault()?.ShortCode)
                    .Where(x => x is not null)
                    .Select(x => x)
            );

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

        public static IEnumerable<ITrace> ToMapLines(this IEnumerable<LocationPair> collection) =>
            collection
                .Select(x =>
                    new ScatterGeo
                    {
                        LocationMode = LocationModeEnum.ISO3,
                        Lon = new List<object> { x.A.Longitude, x.B.Longitude },
                        Lat = new List<object> { x.A.Latitude, x.B.Latitude },
                        Mode = ModeFlag.Lines,
                        Meta = (x.A.Id, x.B.Id)
                    }
                );

        public static IEnumerable<ITrace> ToMapLines(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .ToMapLines();

        public static async IAsyncEnumerable<PartialImprovingIteration> ComputePartialImprovingIterations(
            this List<LocationGeo> startingCollection,
            List<LocationRow> matrix,
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
                startingCollection.ToText(),
                startingDistance,
                startingMapLinesData
            );

            if (startingCollection.HasInsufficientLocations()) yield break;

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

                            var iterationCycle = await evaluatedCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMapLinesData = await iterationCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMatrix = await matrix.HighlightMatrixCyclePairs(iterationCycle, cancellationToken).ConfigureAwait(true);

                            yield return new PartialImprovingIteration(
                                iterationCollection,
                                iterationCycle,
                                iterationMatrix,
                                $"Swap edge {a.ShortCode} with edge {b.ShortCode}",
                                iterationDistance,
                                iterationMapLinesData
                            );
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

        public static async IAsyncEnumerable<PartialImprovingIteration> ComputeAllSwaps(
            this List<LocationGeo> startingCollection,
            double startingDistance,
            List<LocationGeo> optimalCollection,
            double optimalDistance,
            List<LocationRow> matrix,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            if (startingCollection.HasInsufficientLocations()) yield break;

            var minDistance = startingDistance;
            var evaluatedCollection = startingCollection;
            var counter = 0L;
            const long maxIterations = 1_000;

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

                            var iterationCycle = await evaluatedCollection.ToCyclePairs().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMapLinesData = await iterationCycle.ToMapLines().ToListAsync(cancellationToken).ConfigureAwait(true);
                            var iterationMatrix = await matrix.HighlightMatrixCyclePairs(iterationCycle, cancellationToken).ConfigureAwait(true);

                            yield return new PartialImprovingIteration(
                                iterationCollection,
                                iterationCycle,
                                iterationMatrix,
                                $"Swap edge {a.ShortCode} with edge {b.ShortCode}",
                                iterationDistance,
                                iterationMapLinesData
                            );
                        }
                    }
                }

                counter++;
            }
            while (minDistance > optimalDistance && counter <= maxIterations);
        }
    }
}
