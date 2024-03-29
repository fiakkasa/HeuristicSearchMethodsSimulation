﻿namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record LocationToLocation(
        LocationGeo A,
        LocationGeo B,
        string DirectionalKey,
        string ReverseDirectionalKey,
        string Key,
        double DistanceInKilometers,
        long Index,
        bool IsHighlightedDistance
    ) : LocationPair(A, B);
}
