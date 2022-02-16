using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System.Collections.Generic;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions.TravelingSalesMan
{
    public static class TravelingSalesManPreselectedExtensions
    {
        public static IAsyncEnumerable<ITrace> ToPreselectedMarkers(this IEnumerable<LocationGeo> locations) =>
            locations
                .Select((x, i) =>
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
                        HoverTemplate = $"Ordinal: {i + 1}<br />{x.Label} ({x.ShortCode})<br />{nameof(HoverInfoFlag.Lat)}: {x.Latitude}<br />{nameof(HoverInfoFlag.Lon)}: {x.Longitude}",
                        Meta = x.Id
                    }
                )
                .ToAsyncEnumerable<ITrace>();
    }
}
