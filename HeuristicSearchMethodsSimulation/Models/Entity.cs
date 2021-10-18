using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public abstract record Entity
    {
        [BsonId(IdGenerator = typeof(GuidGenerator))]
        [BsonRepresentation(BsonType.ObjectId)]
        public Guid Id { get; set; }
    }
}
