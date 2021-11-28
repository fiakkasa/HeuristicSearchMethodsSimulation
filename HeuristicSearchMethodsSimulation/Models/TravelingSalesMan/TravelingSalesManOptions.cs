namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    public record TravelingSalesManOptions
    {
        public int InitialSliderValue { get; set; } = 1;
        public int MinSliderValue { get; set; } = 1;
        public int MaxSliderValue { get; set; } = 10;
        public int SliderStepValue { get; set; } = 1;
        public int FetchLimit { get; set; } = 100;
        public ChartsOptions Charts { get; set; } = new();
        public MapsOptions Maps { get; set; } = new();
        public int MaxExhaustiveLocationsToCalculate { get; set; } = 7;
    }
}
