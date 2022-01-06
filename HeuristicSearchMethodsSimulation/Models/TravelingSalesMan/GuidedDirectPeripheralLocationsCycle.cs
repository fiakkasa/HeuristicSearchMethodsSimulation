using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Attributes;
using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models.TravelingSalesMan
{
    [BsonIgnoreExtraElements]
    [BsonDiscriminator(nameof(GuidedDirectPeripheralLocationsCycle))]
    public record GuidedDirectPeripheralLocationsCycle : LocationsCycle
    {
        public int NumberOfLocations { get; set; }
        public List<Guid> Collection { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    [CollectionName("LocationsCycles")]
    [BsonKnownTypes(typeof(GuidedDirectPeripheralLocationsCycle))]
    public record LocationsCycle : Entity { }
}
