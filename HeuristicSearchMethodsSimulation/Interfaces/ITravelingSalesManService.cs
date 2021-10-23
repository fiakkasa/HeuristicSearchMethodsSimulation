using HeuristicSearchMethodsSimulation.Models;
using Plotly.Blazor;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces
{
    public interface ITravelingSalesManService
    {
        Task<List<ITrace>> CalculateMapMarkers(List<LocationGeo> locations, CancellationToken cancellationToken = default);
        Task<List<LocationRow>> CalculateMatrix(List<LocationGeo> locations, CancellationToken cancellationToken = default);
        Task<List<LocationGeo>> Fetch(int limit = 1000, CancellationToken cancellationToken = default);
    }
}