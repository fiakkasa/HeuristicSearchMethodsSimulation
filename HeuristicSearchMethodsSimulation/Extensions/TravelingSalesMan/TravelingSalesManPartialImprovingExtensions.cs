using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManPartialImprovingExtensions
    {
        public static async IAsyncEnumerable<PartialImprovingIteration> ComputePartialImprovingIterations(
            this List<LocationGeo> startingCollection,
            List<LocationRow> matrix,
            PartialImprovingMapOptions mapOptions,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            var startingCycle = await startingCollection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
            var startingDistance = await startingCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true);
            var startingMapLinesData = await startingCycle.ToMapLines(cancellationToken).ConfigureAwait(true);
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
                            var previousMatchCycle = await evaluatedCollection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
                            var iterationCycle = await iterationCollection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
                            var iterationMapLinesData = await iterationCycle.ToMapLines(cancellationToken).ConfigureAwait(true);
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

            var lastIterationCycle = await evaluatedCollection.ToCyclePairs(cancellationToken).ConfigureAwait(true);

            yield return new PartialImprovingIteration(
                evaluatedCollection,
                lastIterationCycle,
                await matrix.HighlightMatrixCyclePairs(lastIterationCycle, cancellationToken).ConfigureAwait(true),
                "No further improvement can be made via pairwise exchange.",
                evaluatedCollection.ToText(),
                await lastIterationCycle.CalculateDistanceOfCycle(cancellationToken).ConfigureAwait(true),
                await lastIterationCycle.ToMapLines(cancellationToken).ConfigureAwait(true)
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
                var startingCycle = await startingCollection.ToCyclePairs(cancellationToken).ConfigureAwait(true);
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
    }
}
