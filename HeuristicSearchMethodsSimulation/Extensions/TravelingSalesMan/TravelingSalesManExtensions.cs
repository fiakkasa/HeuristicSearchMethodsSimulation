using HeuristicSearchMethodsSimulation.TravelingSalesMan.Models;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManExtensions
    {
        public static IEnumerable<(LocationGeo, LocationGeo)> ToCyclePairs(this List<LocationGeo> collection)
        {
            if (collection.Count > 1)
            {
                for (int i = 1; i < collection.Count; i++)
                    yield return (collection[i], collection[i - 1]);

                if (collection.Count > 2)
                    yield return (collection[^1], collection[0]);
            }
        }

        public static double CalculateDistancePointToPointInKilometers(this LocationGeo location, LocationGeo otherLocation) =>
            (location, otherLocation) is { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } }
                ? lcGeo.GetDistanceTo(olcGeo) / 1000
                : 0D;

        public static double CalculateDistanceOfCycle(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .Select(x => x.Item1.CalculateDistancePointToPointInKilometers(x.Item2))
                .Sum();
    }
}
