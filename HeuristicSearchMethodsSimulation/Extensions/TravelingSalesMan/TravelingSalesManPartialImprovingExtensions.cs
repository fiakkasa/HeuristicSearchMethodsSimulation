using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using System;
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
                            var previousDistance = minDistance;
                            var previousMapLinesData = previousCycle.ToMapLines();
                            var previousMatrix = matrix.HighlightMatrixCyclePairs(previousCycle);
                            var previousText = evaluatedCollection.ToText();

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
                                    .ToList();

                            var stepOfIterationCounter = 0;

                            foreach (var (First, Second) in cyclesDiff)
                            {
                                stepOfIterationCounter++;

                                var swapPairBLine = Second.ToMapLine();
                                swapPairBLine.Line = new() { Color = mapOptions.LineColor, Width = mapOptions.LineWidth };

                                var iterationMapLinesDataOfStep =
                                    previousMapLinesData
                                        .OfType<ScatterGeo>()
                                        .Where(x =>
                                        {
                                            var (Item1, Item2) = ((Guid, Guid))x.Meta;

                                            return Item1 != First.A.Id && Item2 != First.B.Id;
                                        })
                                        .Append(swapPairBLine)
                                        .ToList<ITrace>();

                                previousMapLinesData.Clear();
                                previousMapLinesData.AddRange(iterationMapLinesDataOfStep);

                                yield return stepOfIterationCounter >= cyclesDiff.Count
                                    ? new PartialImprovingIteration(
                                        iterationCollection,
                                        iterationCycle,
                                        matrix.HighlightMatrixCyclePairs(iterationCycle),
                                        $"Swap edge ({First.A.ShortCode}-{First.B.ShortCode}) with edge ({Second.A.ShortCode}-{Second.B.ShortCode})",
                                        iterationCollection.ToText(),
                                        iterationDistance,
                                        iterationCycle.ToMapLines()
                                    )
                                   : new PartialImprovingIteration(
                                        previousCollection,
                                        previousCycle,
                                        previousMatrix,
                                        $"Swap edge ({First.A.ShortCode}-{First.B.ShortCode}) with edge ({Second.A.ShortCode}-{Second.B.ShortCode})",
                                        previousText,
                                        previousDistance,
                                        previousMapLinesData.Copy()
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
