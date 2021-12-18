using HeuristicSearchMethodsSimulation.Enums;

namespace HeuristicSearchMethodsSimulation.Models
{
    public record MenuItem(string Url, string Title, string ImageUrl, HeuristicMethods Method)
    {
        public bool Disabled { get; set; }
    }
}
