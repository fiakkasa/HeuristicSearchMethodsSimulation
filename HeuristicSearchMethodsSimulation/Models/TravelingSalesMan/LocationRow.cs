using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Models
{
    public record LocationRow(List<LocationToLocation> Collection, string Ylabel, List<string> Xlabels);
}
