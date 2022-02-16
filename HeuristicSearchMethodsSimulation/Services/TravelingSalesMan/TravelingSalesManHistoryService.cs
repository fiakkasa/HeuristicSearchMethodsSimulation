using HeuristicSearchMethodsSimulation.Extensions;
using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Services.TravelingSalesMan
{
    public class TravelingSalesManHistoryService : ITravelingSalesManHistoryService
    {
        private readonly ILogger<TravelingSalesManHistoryService> _logger;
        private readonly Dictionary<int, List<HistoryIteration>> _history = new();

        public IReadOnlyDictionary<int, List<HistoryIteration>> History => _history;

        public TravelingSalesManHistoryService(ILogger<TravelingSalesManHistoryService> logger)
        {
            _logger = logger;
        }

        public async Task SetHistory(HistoryIteration obj, int sliderValue, CancellationToken cancellationToken)
        {
            try
            {
                if (!_history.ContainsKey(sliderValue))
                    _history.Add(sliderValue, new());

                _history[sliderValue] =
                    await _history[sliderValue]
                        .Append(obj)
                        .DistinctBy(x => x.Algo + x.Text)
                        .OrderBy(x => x.DistanceInKilometers)
                        .Take(Consts.MaxNumberOfHistoryLocations)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
