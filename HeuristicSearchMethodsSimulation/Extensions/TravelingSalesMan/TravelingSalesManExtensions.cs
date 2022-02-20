using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManExtensions
    {
        public static List<LocationRow> CalculateMatrix(this IEnumerable<LocationGeo> locations)
        {
            var xLabels = locations.Select(x => $"{x.Label} ({x.ShortCode})").ToList();
            return locations
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
                        YId: location.Id,
                        Xlabels: xLabels
                    );
                })
                .ToList();
        }

        public static List<LocationRow> HighlightMatrixCyclePairs(this List<LocationRow> matrix, List<LocationPair> cyclePairs) =>
            matrix
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
                .ToList();

        public static bool HasInsufficientData<T>(this List<T>? collection) => collection is not { Count: >= Consts.MinNumberOfLocations };

        private static string GetDistanceFormat(bool simple = false) => simple switch
        {
            true => "#0",
            _ => "#0.## Km"
        };

        public static string? ToFormattedDistance(this double? value, bool simple = false) =>
            value?.ToString(GetDistanceFormat(simple));

        public static string ToFormattedDistance(this double value, bool simple = false) =>
            value.ToString(GetDistanceFormat(simple));

        public static string ToKey(this LocationGeo location, LocationGeo otherLocation) =>
            location.Id.CompareTo(otherLocation.Id) <= 0
                ? location.ToDirectionalKey(otherLocation)
                : location.ToReverseDirectionalKey(otherLocation);

        public static string ToDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{location.Id}-{otherLocation.Id}";

        public static string ToReverseDirectionalKey(this LocationGeo location, LocationGeo otherLocation) =>
            $"{otherLocation.Id}-{location.Id}";

        public static string ToText(this List<LocationGeo> collection, string separator = " > ", string? customLastElemText = default) =>
            string.Join(
                separator,
                collection
                    .Select(x => x?.ShortCode)
                    .Append(customLastElemText?.Trim() is { Length: > 0 } ? customLastElemText : collection.FirstOrDefault()?.ShortCode)
                    .Where(x => x is not null)
                    .Select(x => x)
            );

        public static List<LocationPair> ToPartialCyclePairs(this List<LocationGeo> collection)
        {
            return ToPartialCyclePairs(collection).ToList();

            static IEnumerable<LocationPair> ToPartialCyclePairs(List<LocationGeo> collection)
            {
                if (collection.Count >= 2)
                {
                    for (int i = 1; i < collection.Count; i++)
                        yield return new(collection[i - 1], collection[i]);
                }
            }
        }

        public static List<LocationPair> ToCyclePairs(this List<LocationGeo> collection)
        {
            return ToCyclePairs(collection).ToList();

            static IEnumerable<LocationPair> ToCyclePairs(List<LocationGeo> collection)
            {
                if (collection.Count >= Consts.MinNumberOfLocations)
                {
                    for (int i = 1; i < collection.Count; i++)
                        yield return new(collection[i - 1], collection[i]);

                    yield return new(collection[^1], collection[0]);
                }
            }
        }

        public static double CalculateDistancePointToPointInKilometers(this LocationGeo location, LocationGeo otherLocation) =>
            (location, otherLocation) is { location.Geo: { } lcGeo, otherLocation.Geo: { } olcGeo }
                ? lcGeo.GetDistanceTo(olcGeo) / 1000D
                : 0D;

        public static double CalculateDistanceOfCycle(this List<LocationPair> collection) =>
            collection
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .Sum();

        public static double CalculateDistanceOfCycle(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .Select(x => x.A.CalculateDistancePointToPointInKilometers(x.B))
                .Sum();

        public static ScatterGeo ToMapLine(this LocationPair x) =>
            new()
            {
                LocationMode = LocationModeEnum.ISO3,
                Lon = new List<object> { x.A.Longitude, x.B.Longitude },
                Lat = new List<object> { x.A.Latitude, x.B.Latitude },
                Mode = ModeFlag.Lines,
                Meta = (x.A.Id, x.B.Id)
            };

        public static List<ITrace> ToMapLines(this IEnumerable<LocationPair> collection) =>
            collection.Select(x => x.ToMapLine()).ToList<ITrace>();

        public static List<ITrace> ToMapLines(this List<LocationGeo> collection) =>
            collection
                .ToCyclePairs()
                .ToMapLines();

        public static List<ITrace> ToMapLines(this List<LocationGeo> collection, IEnumerable<ITrace> append) =>
            collection
                .ToCyclePairs()
                .Select(x => x.ToMapLine())
                .Concat(append)
                .ToList();

        public static List<long> CalculateNumberOfUniqueRoutesPerNumberOfLocations(this int numberOfLocations) =>
            Enumerable.Range(0, numberOfLocations)
                .Select(i => Enumerable.Range(1, i).Aggregate(1L, (f, x) => f * x) / 2) // (n − 1)! / 2
                .Take(numberOfLocations)
                .ToList();

        public static List<ITrace> ToDefaultMarkers(this IEnumerable<LocationGeo> locations) =>
            locations
                .Select(x =>
                    new ScatterGeo
                    {
                        LocationMode = LocationModeEnum.ISO3,
                        Lon = new List<object> { x.Longitude },
                        Lat = new List<object> { x.Latitude },
                        Mode = ModeFlag.Markers | ModeFlag.Text,
                        Text = x.ShortCode,
                        TextPosition = TextPositionEnum.TopCenter,
                        Name = $"{x.Label} ({x.ShortCode})",
                        HoverLabel = new() { NameLength = 0 },
                        HoverTemplate = $"{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                        Meta = x.Id
                    }
                )
                .ToList<ITrace>();
    }
}
