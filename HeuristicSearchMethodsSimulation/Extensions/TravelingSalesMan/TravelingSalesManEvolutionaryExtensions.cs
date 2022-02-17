using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.CodeAnalysis;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManEvolutionaryExtensions
    {
        public static IAsyncEnumerable<ITrace> ToEvolutionaryMarkers(this IEnumerable<LocationGeo> locations, EvolutionaryMapOptions options) =>
            locations
                .Select((x, i) =>
                    i > 0
                        ? new ScatterGeo
                        {
                            LocationMode = LocationModeEnum.ISO3,
                            Lon = new List<object> { x.Longitude },
                            Lat = new List<object> { x.Latitude },
                            Mode = ModeFlag.Markers | ModeFlag.Text,
                            Marker = new()
                            {
                                Color = options.MarkerColor,
                                Symbol = options.MarkerSymbol
                            },
                            Text = $"{i} - {x.ShortCode}",
                            TextPosition = TextPositionEnum.TopCenter,
                            Name = $"{x.Label} ({x.ShortCode})",
                            HoverLabel = new() { NameLength = 0 },
                            HoverTemplate = $"Ordinal: {i}<br />{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                            Meta = x.Id
                        }
                        : new ScatterGeo
                        {
                            LocationMode = LocationModeEnum.ISO3,
                            Lon = new List<object> { x.Longitude },
                            Lat = new List<object> { x.Latitude },
                            Mode = ModeFlag.Markers | ModeFlag.Text,
                            Marker = new()
                            {
                                Color = options.FirstMarkerColor,
                                Symbol = options.FirstMarkerSymbol
                            },
                            Text = x.ShortCode,
                            TextPosition = TextPositionEnum.TopCenter,
                            Name = $"{x.Label} ({x.ShortCode})",
                            HoverLabel = new() { NameLength = 0 },
                            HoverTemplate = $"{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                            Meta = x.Id
                        }
                )
                .ToAsyncEnumerable<ITrace>();

        public static IEnumerable<EvolutionaryNodes> ComputeEvolutionaryOffsprings(this IEnumerable<EvolutionaryNodes> collection, int numberOfBits)
        {
            var i = 0;

            foreach (var nodes in collection.Select(x => x.Nodes))
            {
                var seed = collection
                    .Skip(i switch
                    {
                        0 => 1,
                        _ when i == nodes.Count - 1 => nodes.Count - 2,
                        _ => i - 1
                    })
                    .FirstOrDefault()?
                    .Nodes;
                if (seed is not { }) break;

                var tail = nodes.Skip(numberOfBits).ToList();
                var newTail = new List<EvolutionaryNode>();

                for (var j = 0; j < seed.Count; j++)
                {
                    if (!tail.Contains(seed[j])) continue;

                    newTail.Add(seed[j]);
                }

                var result =
                    nodes
                        .Take(numberOfBits)
                        .Concat(newTail)
                        .ToList();

                yield return new()
                {
                    Id = Guid.NewGuid(),
                    Nodes = result,
                    Text = result.ToText(),
                    DistanceInKilometers =
                        result
                            .ConvertAll(x => x.Location)
                            .CalculateDistanceOfCycle()
                };

                i++;
            }
        }

        public static IEnumerable<EvolutionaryNodes> ComputeEvolutionaryRanks(this IEnumerable<EvolutionaryNodes> collection)
        {
            if (!collection.Any()) yield break;

            var min =
                collection
                    .Where(x => x.DistanceInKilometers > 0)
                    .MinBy(x => x.DistanceInKilometers)?
                    .DistanceInKilometers is { } mn and > 0
                    ? mn
                    : 0D;
            var max =
                collection
                    .Where(x => x.DistanceInKilometers > 0)
                    .MaxBy(x => x.DistanceInKilometers)?
                    .DistanceInKilometers is { } mx && min < mx
                    ? mx
                    : min + 1;

            var minMax = max - min;

            foreach (var item in collection)
            {
                yield return item with
                {
                    Rank = ((item.DistanceInKilometers - min) / minMax) switch
                    {
                        { } value and >= 0D and <= 1D => value,
                        < 0D => 0D,
                        _ => 1
                    }
                };
            }
        }
    }
}
