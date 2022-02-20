using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManGuidedDirectService : ITravelingSalesManBaseService
    {
        bool EnableBuilders { get; }

        GuidedDirectItem? GuidedDirectItem { get; set; }

        Task SetGuidedDirectSelection(Guid locationId);
        Task SetGuidedDirectSelection(LocationGeo location);
        Task ResetGuidedDirect();
        void SetGuidedDirectRule(int rule);
    }
}