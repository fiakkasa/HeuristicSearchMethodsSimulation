using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public abstract record Entity
    {
        [BsonId(IdGenerator = typeof(GuidGenerator))]
        public virtual Guid Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public virtual DateTimeOffset CreatedAt { get; set; }

        [BsonRepresentation(BsonType.String)]
        public virtual DateTimeOffset UpdatedAt { get; set; }
    }
}
