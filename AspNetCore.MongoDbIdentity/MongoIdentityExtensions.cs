using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System;

namespace AspNetCore.MongoDbIdentity
{
    public static class MongoIdentityExtensions
    {
        public static IdentityBuilder AddMongoDbStores<TUser, TRole>(this IdentityBuilder builder)
            where TUser : MongoIdentityUser
            where TRole : MongoIdentityRole
        {
            return AddMongoDbStores<TUser, TRole>(builder,
                p => p.GetRequiredService<IMongoCollection<TUser>>(),
                p => p.GetRequiredService<IMongoCollection<TRole>>());
        }

        public static IdentityBuilder AddMongoDbStores<TUser, TRole>(this IdentityBuilder builder, string mongoConnectionString)
            where TUser : MongoIdentityUser
            where TRole : MongoIdentityRole
        {
            var url = new MongoUrl(mongoConnectionString);
            if (string.IsNullOrEmpty(url.DatabaseName))
            {
                throw new ArgumentException("Connection string must contains database name.", nameof(mongoConnectionString));
            }

            var client = new MongoClient(url);
            var database = client.GetDatabase(url.DatabaseName);
            return AddMongoDbStores<TUser, TRole>(
                builder,
                _ => database.GetCollection<TUser>("users"),
                _ => database.GetCollection<TRole>("roles"));
        }

        public static IdentityBuilder AddMongoDbStores<TUser, TRole>(this IdentityBuilder builder,
            Func<IServiceProvider, IMongoCollection<TUser>> userMongoFactory,
            Func<IServiceProvider, IMongoCollection<TRole>> roleMongoFactory)
            where TUser : MongoIdentityUser
            where TRole : MongoIdentityRole
        {
            builder.Services.AddSingleton<IUserStore<TUser>>(p => new MongoUserStore<TUser>(userMongoFactory(p)));
            builder.Services.AddSingleton<IRoleStore<TRole>>(p => new MongoRoleStore<TRole>(roleMongoFactory(p)));

            return builder;
        }
    }
}
