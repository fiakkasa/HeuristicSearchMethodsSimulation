using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManExhaustiveService : ITravelingSalesManBaseService
    {
        public ExhaustiveItem? ExhaustiveItem { get; set; }

        void SetExhaustiveItem(ExhaustiveIteration item);
    }
}