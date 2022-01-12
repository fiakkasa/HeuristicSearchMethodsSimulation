using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record LocationRow(List<LocationToLocation> Collection, string Ylabel, Guid YId, List<string> Xlabels);
}
