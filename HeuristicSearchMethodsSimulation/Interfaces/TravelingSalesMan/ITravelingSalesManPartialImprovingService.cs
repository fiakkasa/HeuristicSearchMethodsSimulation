using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManPartialImprovingService : ITravelingSalesManBaseService
    {
        PartialImprovingItem? PartialImprovingItem { get; set; }

        void ResetPartialImproving();
        void PartialImprovingNextIteration();
    }
}