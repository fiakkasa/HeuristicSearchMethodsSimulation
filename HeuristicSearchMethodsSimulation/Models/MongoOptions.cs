namespace HeuristicSearchMethodsSimulation.Models
{
    public record MongoOptions
    {
        public MongoDatabases Databases { get; set; } = new();
    }
}
