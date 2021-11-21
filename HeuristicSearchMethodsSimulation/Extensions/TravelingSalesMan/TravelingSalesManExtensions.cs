using HeuristicSearchMethodsSimulation.TravelingSalesMan.Models;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System.Collections.Generic;
using System.Linq;

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

        public static IEnumerable<(LocationGeo A, LocationGeo B)> ToCyclePairs(this List<LocationGeo> collection)
        {
            if (collection.Count > 1)
            {
                for (int i = 1; i < collection.Count; i++)
                    yield return (collection[i - 1], collection[i]);

                if (collection.Count > 2)
                    yield return (collection[0], collection[^1]);
            }
        }

        public static double CalculateDistancePointToPointInKilometers(this LocationGeo location, LocationGeo otherLocation) =>
            (location, otherLocation) is { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } }
                ? lcGeo.GetDistanceTo(olcGeo) / 1000
                : 0D;

        public static double CalculateDistanceOfCycle(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .Sum();

        public static List<ITrace> ToMapLines(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .Select(x =>
                    new ScatterGeo
                    {
                        LocationMode = LocationModeEnum.ISO3,
                        Lon = new List<object> { x.A.Longitude, x.B.Longitude },
                        Lat = new List<object> { x.A.Latitude, x.B.Latitude },
                        Mode = ModeFlag.Lines,
                        Meta = (x.A.Id, x.B.Id)
                    }
                )
                .ToList<ITrace>();
    }
}
