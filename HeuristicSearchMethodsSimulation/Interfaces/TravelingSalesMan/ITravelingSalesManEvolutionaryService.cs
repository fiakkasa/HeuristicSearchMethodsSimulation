using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManEvolutionaryService : ITravelingSalesManBaseService
    {
        bool EnableBuilders { get; }

        EvolutionaryItem? EvolutionaryItem { get; set; }

        void SetEvolutionaryLocation(Guid locationId);
        void SetEvolutionaryLocation(EvolutionaryNode item);
        Task SetEvolutionaryStep(int step);
        void ResetEvolutionary();
        Task SetEvolutionarySpin();
    }
}