using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManPartialRandomService : ITravelingSalesManBaseService
    {
        PartialRandomItem? PartialRandomItem { get; set; }

        Task ClearPartialRandomBuilder();
        Task SetPartialRandomLocation(Guid locationId);
        Task SetPartialRandomLocation(LocationGeo item);

        Task SetPartialRandomItem(PartialRandomIteration item);
    }
}