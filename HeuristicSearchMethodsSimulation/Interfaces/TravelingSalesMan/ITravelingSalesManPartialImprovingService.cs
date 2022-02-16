using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManPartialImprovingService : ITravelingSalesManBaseService
    {
        PartialImprovingItem? PartialImprovingItem { get; set; }

        Task ResetPartialImproving();
        Task PartialImprovingNextIteration();
    }
}