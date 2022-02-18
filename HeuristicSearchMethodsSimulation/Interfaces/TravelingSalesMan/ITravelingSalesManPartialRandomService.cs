using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using System;

namespace HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan
{
    public interface ITravelingSalesManPartialRandomService : ITravelingSalesManBaseService
    {
        PartialRandomItem? PartialRandomItem { get; set; }

        void ClearPartialRandomBuilder();
        void SetPartialRandomLocation(Guid locationId);
        void SetPartialRandomLocation(LocationGeo item);
        void SetPartialRandomItem(PartialRandomIteration item);
    }
}