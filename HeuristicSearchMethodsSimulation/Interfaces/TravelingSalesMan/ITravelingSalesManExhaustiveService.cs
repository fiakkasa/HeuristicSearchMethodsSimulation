using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManExhaustiveService : ITravelingSalesManBaseService
    {
        public ExhaustiveItem? ExhaustiveItem { get; set; }

        Task SetExhaustiveItem(ExhaustiveIteration item);
    }
}