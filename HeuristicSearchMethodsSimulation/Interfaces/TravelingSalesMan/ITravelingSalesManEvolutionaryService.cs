using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManEvolutionaryService : ITravelingSalesManBaseService
    {
        EvolutionaryItem? EvolutionaryItem { get; set; }

        Task SetEvolutionaryLocation(Guid locationId);
        Task SetEvolutionaryLocation(EvolutionaryNode item);
        Task SetEvolutionaryStep(int step);

        Task ResetEvolutionary();
        Task SetEvolutionarySpin();
    }
}