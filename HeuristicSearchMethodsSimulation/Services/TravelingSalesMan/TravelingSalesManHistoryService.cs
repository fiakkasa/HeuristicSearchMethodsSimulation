using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Services.TravelingSalesMan
{
    public class TravelingSalesManHistoryService : ITravelingSalesManHistoryService
    {
        private readonly ILogger<TravelingSalesManHistoryService> _logger;

        public Dictionary<int, List<HistoryIteration>> History { get; } = new();

        public TravelingSalesManHistoryService(ILogger<TravelingSalesManHistoryService> logger)
        {
            _logger = logger;
        }

        public void SetHistory(HistoryIteration obj, int sliderValue)
        {
            try
            {
                if (!History.ContainsKey(sliderValue))
                    History.Add(sliderValue, new());

                History[sliderValue] =
                    History[sliderValue]
                        .Append(obj)
                        .DistinctBy(x => x.Algo + x.Text)
                        .OrderBy(x => x.DistanceInKilometers)
                        .Take(Consts.MaxNumberOfHistoryLocations)
                        .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
