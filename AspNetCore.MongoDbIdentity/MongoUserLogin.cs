using System;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.MongoDbIdentity
{
    public class MongoUserLogin : IEquatable<MongoUserLogin>, IEquatable<UserLoginInfo>
    {
        public MongoUserLogin(UserLoginInfo userLoginInfo)
        {
            if (userLoginInfo == null)
            {
                throw new ArgumentNullException(nameof(userLoginInfo));
            }

            LoginProvider = userLoginInfo.LoginProvider;
            ProviderKey = userLoginInfo.ProviderKey;
            ProviderDisplayName = userLoginInfo.ProviderDisplayName;
        }

        public string LoginProvider { get; }

        public string ProviderKey { get; }

        public string ProviderDisplayName { get; }

        public bool Equals(MongoUserLogin other)
        {
            return string.Equals(other?.LoginProvider, LoginProvider)
                   && string.Equals(other?.ProviderKey, ProviderKey);
        }

        public bool Equals(UserLoginInfo other)
        {
            return string.Equals(other?.LoginProvider, LoginProvider)
                   && string.Equals(other?.ProviderKey, ProviderKey);
        }

        public UserLoginInfo ToUserLoginInfo()
        {
            return new UserLoginInfo(LoginProvider, ProviderKey, ProviderDisplayName);
        }
    }
}
