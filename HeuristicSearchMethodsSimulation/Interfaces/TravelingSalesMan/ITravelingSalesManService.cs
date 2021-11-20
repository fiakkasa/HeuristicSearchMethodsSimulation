using HeuristicSearchMethodsSimulation.Enums;
using HeuristicSearchMethodsSimulation.TravelingSalesMan.Models;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.TravelingSalesMan.Interfaces
{
	public interface ITravelingSalesManService
	{
		TravelingSalesManAlgorithms Algorithm { get; }
		bool HasLocations { get; }
		bool IsInit { get; }
		bool Loading { get; }
		List<LocationGeo> Locations { get; }
		List<LocationGeo> LocationsBySelection { get; }
		List<ITrace> MapChartData { get; }
		List<LocationRow> Matrix { get; }
		int MaxSliderValue { get; }
		int MinSliderValue { get; }
		List<long> NumberOfUniqueRoutesPerNumberOfLocations { get; }
		long NumberOfUniqueRoutes { get; }
		int SliderStepValue { get; }
		int SliderValue { get; }
		double? TotalDistanceInKilometers { get; }
		TravelingSalesManMapOptions MapOptions { get; }
		bool MaxExhaustiveLocationsToCalculateReached { get; }
		List<ExhaustiveItem> ExhaustiveItems { get; }
		ExhaustiveItem? SelectedExhaustiveItem { get; }

		event Action? OnStateChange;

		Task Init(TravelingSalesManAlgorithms algo);
		Task Refresh();
		Task UpdateState(int sliderValue);
	}
}