using Microsoft.Extensions.Configuration;
using System;

namespace MirrorManager.Web.MSAL.Configuration
{
    public interface ITokenCacheConfig
    {
        string ConnectionString { get; }
    }

    public class TokenCacheConfig : ITokenCacheConfig
    {
        private IConfigurationRoot _configuration;
        public TokenCacheConfig(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }
        public string ConnectionString
        {
            get
            {
                return _configuration["TOKEN_STORAGE"];
            }
        }
    }
}