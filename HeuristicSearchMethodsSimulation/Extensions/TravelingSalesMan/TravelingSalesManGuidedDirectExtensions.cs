using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Plotly.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManGuidedDirectExtensions
    {
        public static IEnumerable<GuidedDirectIteration> ComputeGuidedDirectIterationsFromGuidedDirectCollection(
            this List<LocationGeo> collection,
            List<LocationRow> matrix,
            List<ITrace> markers
        )
        {
            if (collection.HasInsufficientData()) yield break;

            var iterationCollection = new List<LocationGeo>();

            for (int i = 0; i < collection.Count; i++)
            {
                var item = collection[i] with { };
                iterationCollection.Add(item);

                var iterationCyclePairs = iterationCollection.ToPartialCyclePairs();
                var iterationMatrix = matrix.HighlightMatrixCyclePairs(iterationCyclePairs);
                var iterationDistance = iterationCyclePairs.CalculateDistanceOfCycle();
                var iterationMapLineData = iterationCyclePairs.ToMapLines();

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

            var computedCycle = collection.ToCyclePairs();
            var computedMatrix = matrix.HighlightMatrixCyclePairs(computedCycle);
            var computedDistance = computedCycle.CalculateDistanceOfCycle();
            var computedMapLinesData = computedCycle.ToMapLines();

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
