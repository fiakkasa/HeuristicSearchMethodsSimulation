using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManPreselectedService : ITravelingSalesManBaseService
    {
        PreselectedItem? PreselectedItem { get; set; }
    }
}