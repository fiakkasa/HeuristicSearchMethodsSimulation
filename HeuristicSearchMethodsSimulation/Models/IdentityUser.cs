using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;
using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    [CollectionName("Users")]
    public class IdentityUser : MongoIdentityUser<Guid> { }
}
