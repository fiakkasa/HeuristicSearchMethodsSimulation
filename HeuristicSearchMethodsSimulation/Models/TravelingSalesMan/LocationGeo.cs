using AutoMapper;
using GeoCoordinatePortable;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    [AutoMap(typeof(Location), ReverseMap = true)]
    public record LocationGeo : Location
    {
        public GeoCoordinate? Geo { get; set; }
    }
}
