using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AspNetCore.MongoDbIdentity
{
    public class MongoIdentityRole
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
    }
}
