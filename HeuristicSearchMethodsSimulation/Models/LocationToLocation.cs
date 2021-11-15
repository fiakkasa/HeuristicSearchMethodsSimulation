using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public record LocationToLocation(LocationGeo A, LocationGeo B, string DirectionalKey, string ReverseDirectionalKey, string Key, double DistanceInKilometers, long Index, long OrdinalFromOrigin, bool IsShortestDistance) : IComparable<LocationToLocation>
    {
        public int CompareTo(LocationToLocation? other) => OrdinalFromOrigin.CompareTo(other?.OrdinalFromOrigin ?? long.MaxValue);
    }
}
