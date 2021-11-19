﻿using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record ExhaustiveItem(List<LocationGeo> Collection, string Text, double DistanceInKilometers, string Id);
}
