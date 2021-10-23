using MongoDB.Driver;
using MongoDbGenericRepository.Attributes;
using System;
using System.Linq;

namespace HeuristicSearchMethodsSimulation.Extensions
{
    public static class MongoExtensions
    {
        public static IMongoCollection<T> GetCollection<T>(this IMongoClient client, string database) where T : class =>
            client.GetDatabase(database).GetCollection<T>(
               Attribute.GetCustomAttributes(typeof(T))
                   .OfType<CollectionNameAttribute>()
                   .Take(1)
                   .Select(x => x.Name)
                   .FirstOrDefault()
           );
    }
}
