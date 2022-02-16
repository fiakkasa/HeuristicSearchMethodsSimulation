using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Plotly.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManGuidedDirectExtensions
    {
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
    }
}
