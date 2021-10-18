﻿using MongoDB.Bson.Serialization.Attributes;

namespace HeuristicSearchMethodsSimulation.Models
{
    [BsonIgnoreExtraElements]
    public record Location : Entity
    {
        public string Label { get; set; } = string.Empty;

        public string ShortCode { get; set; } = string.Empty;

        public string? Description { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public int Ordinal { get; set; } = int.MaxValue;
    }
}
