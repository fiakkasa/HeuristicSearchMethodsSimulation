using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    public abstract record Entity
    {
        [BsonId(IdGenerator = typeof(GuidGenerator))]
        public virtual Guid Id { get; set; }

        public virtual DateTimeOffset CreatedAt { get; set; }

        public virtual DateTimeOffset UpdatedAt { get; set; }
    }
}
