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
            get { return settings.Get<bool>("Octopus.Server.Proxy.UseDefaultProxy", true); }
            set { settings.Set("Octopus.Server.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get("Octopus.Server.Proxy.ProxyUsername", string.Empty); }
            set { settings.Set("Octopus.Server.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get<string>("Octopus.Server.Proxy.ProxyPassword", machineKeyEncrypted: true); }
            set { settings.Set("Octopus.Server.Proxy.ProxyPassword", value, true); }
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
    }
}