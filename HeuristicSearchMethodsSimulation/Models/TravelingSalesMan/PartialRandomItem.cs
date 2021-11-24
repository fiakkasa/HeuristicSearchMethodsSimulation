using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record PartialRandomItem(List<LocationGeo> Collection, string Text, double DistanceInKilometers, Guid Id);
}
