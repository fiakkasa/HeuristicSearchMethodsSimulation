using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManNoneService : ITravelingSalesManBaseService
    {
        NoneItem? NoneItem { get; set; }
    }
}