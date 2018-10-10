using System;
using Octopus.Configuration;

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
            get { return settings.Get<bool>("Octopus.Proxy.UseDefaultProxy", true); }
            set { settings.Set("Octopus.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get("Octopus.Proxy.ProxyUsername", string.Empty); }
            set { settings.Set("Octopus.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get<string>("Octopus.Proxy.ProxyPassword", machineKeyEncrypted: true); }
            set { settings.Set("Octopus.Proxy.ProxyPassword", value, true); }
        }

        public string CustomProxyHost
        {
            get { return settings.Get("Octopus.Proxy.ProxyHost", string.Empty); }
            set { settings.Set("Octopus.Proxy.ProxyHost", value); }
        }

        public int CustomProxyPort
        {
            get { return settings.Get("Octopus.Proxy.ProxyPort", 80); }
            set { settings.Set("Octopus.Proxy.ProxyPort", value); }
        }
    }
}