using System.Security.Cryptography;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class PollingProxyConfiguration : IPollingProxyConfiguration
    {
        readonly IKeyValueStore settings;

        public PollingProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get { return settings.Get("Octopus.Server.Proxy.UseDefaultProxy", true); }
            set { settings.Set("Octopus.Server.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get("Octopus.Server.Proxy.ProxyUsername", string.Empty); }
            set { settings.Set("Octopus.Server.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get("Octopus.Server.Proxy.ProxyPassword", protectionScope: DataProtectionScope.LocalMachine); }
            set { settings.Set("Octopus.Server.Proxy.ProxyPassword", value, DataProtectionScope.LocalMachine); }
        }

        public string CustomProxyHost
        {
            get { return settings.Get("Octopus.Server.Proxy.ProxyHost", string.Empty); }
            set { settings.Set("Octopus.Server.Proxy.ProxyHost", value); }
        }

        public int CustomProxyPort
        {
            get { return settings.Get("Octopus.Server.Proxy.ProxyPort", 80); }
            set { settings.Set("Octopus.Server.Proxy.ProxyPort", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}