using System;
using System.Security.Cryptography;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration
    {
        readonly IKeyValueStore settings;

        public ProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get { return settings.Get("Octopus.Proxy.UseDefaultProxy", true); }
            set { settings.Set("Octopus.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get("Octopus.Proxy.ProxyUsername", string.Empty); }
            set { settings.Set("Octopus.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get("Octopus.Proxy.ProxyPassword", protectionScope: DataProtectionScope.LocalMachine); }
            set { settings.Set("Octopus.Proxy.ProxyPassword", value, DataProtectionScope.LocalMachine); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}