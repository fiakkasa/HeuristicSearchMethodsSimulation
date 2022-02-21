using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Plotly.Blazor;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManPartialImprovingExtensions
    {
        public static IEnumerable<PartialImprovingIteration> ComputePartialImprovingIterations(
            this List<LocationGeo> startingCollection,
            List<LocationRow> matrix,
            MapOptions mapOptions
        )
        {
            var startingCycle = startingCollection.ToCyclePairs();
            var startingDistance = startingCycle.CalculateDistanceOfCycle();
            var startingMapLinesData = startingCycle.ToMapLines();
            var startingMatrix = matrix.HighlightMatrixCyclePairs(startingCycle);

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

                        var iterationDistance = iterationCollection.CalculateDistanceOfCycle();

                        if (iterationDistance < minDistance)
                        {
                            var previousCollection = evaluatedCollection.Copy();
                            var previousCycle = previousCollection.ToCyclePairs();
                            var iterationCycle = iterationCollection.ToCyclePairs();

                            var pairsToKeep =
                                iterationCycle
                                    .Select(x => x.A.ToKey(x.B))
                                    .ExceptBy(previousCycle.Select(x => x.A.ToKey(x.B)), x => x)
                                    .ToList();

                            var cyclesDiff =
                                previousCycle
                                    .Zip(iterationCycle)
                                    .Join(
                                        pairsToKeep,
                                        ecc => ecc.Second.A.ToKey(ecc.Second.B),
                                        ptk => ptk,
                                        (ecc, _) => ecc
                                    )
                                    .Chunk(2)
                                    .Where(x => x.Length == 2)
                                    .Select(x => (x[0].First, x[0].Second, x[1].First, x[1].Second))
                                    .ToList();

                            foreach (var (First0, Second0, First1, Second1) in cyclesDiff)
                            {
                                yield return new PartialImprovingIteration(
                                    previousCollection,
                                    previousCycle,
                                    matrix.HighlightMatrixCyclePairs(previousCycle),
                                    $"Swap edge ({First0.A.ShortCode}-{First0.B.ShortCode}) with edge ({Second1.A.ShortCode}-{Second1.B.ShortCode}) and " +
                                    $"edge ({First1.A.ShortCode}-{First1.B.ShortCode}) with edge ({Second0.A.ShortCode}-{Second0.B.ShortCode}).",
                                    previousCollection.ToText(),
                                    minDistance,
                                    previousCycle.ToMapLines()
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

            var lastIterationCycle = evaluatedCollection.ToCyclePairs();

            yield return new PartialImprovingIteration(
                evaluatedCollection,
                lastIterationCycle,
                matrix.HighlightMatrixCyclePairs(lastIterationCycle),
                "No further improvement can be made via pairwise exchange.",
                evaluatedCollection.ToText(),
                lastIterationCycle.CalculateDistanceOfCycle(),
                lastIterationCycle.ToMapLines()
            );
        }

        public static (string Route, double DistanceInKilometers) ComputePartialImprovingSolution(this List<LocationGeo> startingCollection)
        {
            return Compute(startingCollection).LastOrDefault();

            static IEnumerable<(string Route, double DistanceInKilometers)> Compute(List<LocationGeo> startingCollection)
            {
                var startingCycle = startingCollection.ToCyclePairs();
                var startingDistance = startingCycle.CalculateDistanceOfCycle();

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

                            var iterationDistance = iterationCollection.CalculateDistanceOfCycle();

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
    }
}
