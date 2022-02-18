using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManExhaustiveExtensions
    {
        public static async Task<List<ExhaustiveIteration>> CalculateExhaustiveIterations(this List<LocationGeo> locations, CancellationToken cancellationToken)
        {
            if (locations.HasInsufficientData()) return new();

            return await Task.Run(
                () =>
                    CalculateExhaustivePermutations(locations, locations[0].Id)
                        .Select(collection => new ExhaustiveIteration(collection, collection.ToText(), collection.CalculateDistanceOfCycle(), Guid.NewGuid()))
                        .GroupBy(x => x.DistanceInKilometers.ToFormattedDistance())
                        .Select(x => x.FirstOrDefault())
                        .OfType<ExhaustiveIteration>()
                        .ToList(),
                    cancellationToken
                )
                .ConfigureAwait(true);

            static IEnumerable<List<LocationGeo>> CalculateExhaustivePermutations(List<LocationGeo> collection, Guid id)
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
        }
    }
}
