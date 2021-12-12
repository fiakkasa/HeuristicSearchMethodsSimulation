using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record ExhaustiveIteration(List<LocationGeo> Collection, string Text, double DistanceInKilometers, Guid Id);
}
