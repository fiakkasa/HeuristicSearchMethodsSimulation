using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;
using System;

namespace HeuristicSearchMethodsSimulation.Models
{
    [CollectionName("Roles")]
    public class IdentityRole : MongoIdentityRole<Guid> { }
}
