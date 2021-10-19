using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public record LocationToLocation(LocationGeo A, LocationGeo B, string DirectionalKey, string ReverseDirectionalKey, string Key, double DistanceInMeters, long Index, long OrdinalFromOrigin) : IComparable<LocationToLocation>
    {
        public int CompareTo(LocationToLocation? other) => OrdinalFromOrigin.CompareTo(other?.OrdinalFromOrigin ?? long.MaxValue);
    }
}
