using HeuristicSearchMethodsSimulation.TravelingSalesMan.Models;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManExtensions
    {
        public static string ToKey(this LocationGeo location, LocationGeo otherLocation) =>
            location.ShortCode.CompareTo(otherLocation.ShortCode) <= 0
                ? location.ToDirectionalKey(otherLocation)
                : location.ToReverseDirectionalKey(otherLocation);

        public static string ToDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{location.ShortCode}-{otherLocation.ShortCode}";

        public static string ToReverseDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{otherLocation.ShortCode}-{location.ShortCode}";

        public static string ToText(this List<LocationGeo> collection, string separator = " > ") =>
            string.Join(
                separator,
                collection
                    .Append(collection.FirstOrDefault())
                    .Where(x => x?.ShortCode is not null)
                    .Select(x => x!.ShortCode)
            );

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
    }
}
