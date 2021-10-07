using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace AspNetCore.MongoDbIdentity
{
    public class MongoIdentityUser : IdentityUser<ObjectId>
    {
        public MongoIdentityUser()
        {
            Logins = new List<MongoUserLogin>();
            Claims = new List<MongoUserClaim>();
            Roles = new List<string>();
        }

        public override ObjectId Id { get; set; }

        public virtual List<MongoUserLogin> Logins { get; set; }

        public virtual List<MongoUserClaim> Claims { get; set; }

        public virtual List<string> Roles { get; set; }

        public virtual void AddLogin(UserLoginInfo userLoginInfo)
        {
            if (userLoginInfo == null)
            {
                throw new ArgumentNullException(nameof(userLoginInfo));
            }

            AddLogin(new MongoUserLogin(userLoginInfo));
        }

        public virtual void AddLogin(MongoUserLogin userLogin)
        {
            if (userLogin == null)
            {
                throw new ArgumentNullException(nameof(userLogin));
            }

            Logins.Add(userLogin);
        }

        public virtual void RemoveLogin(string loginProvider, string providerKey)
        {
            Logins.RemoveAll(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey);
        }

        public virtual void AddClaim(Claim claim)
        {
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            AddClaim(new MongoUserClaim(claim));
        }

        public virtual void AddClaim(MongoUserClaim userClaim)
        {
            if (userClaim == null)
            {
                throw new ArgumentNullException(nameof(userClaim));
            }

            Claims.Add(userClaim);
        }

        public virtual void RemoveClaim(Claim claim)
        {
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            Claims.RemoveAll(c => c.Type == claim.Type && c.Value == claim.Value);
        }

        public virtual void ReplaceClaim(Claim claim, Claim newClaim)
        {
            var claimExists = Claims
                .Any(c => c.Type == claim.Type && c.Value == claim.Value);
            if (!claimExists)
            {
                return;
            }

            RemoveClaim(claim);
            AddClaim(newClaim);
        }

        public virtual void AddRole(string role)
        {
            Roles.Add(role);
        }

        public virtual void RemoveRole(string role)
        {
            Roles.Add(role);
        }
    }
}
