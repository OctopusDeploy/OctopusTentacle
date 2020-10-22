#nullable enable
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration
{
    internal class PollingProxyConfiguration : IPollingProxyConfiguration
    {
        readonly IKeyValueStore settings;

        public PollingProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy => settings.Get<bool>("Octopus.Server.Proxy.UseDefaultProxy", true);

        public string? CustomProxyUsername => settings.Get("Octopus.Server.Proxy.ProxyUsername", string.Empty);

        public string? CustomProxyPassword => settings.Get<string>("Octopus.Server.Proxy.ProxyPassword", protectionLevel: ProtectionLevel.MachineKey);

        public string? CustomProxyHost => settings.Get("Octopus.Server.Proxy.ProxyHost", string.Empty);

        public int CustomProxyPort => settings.Get("Octopus.Server.Proxy.ProxyPort", 80);
    }

    class WritablePollingProxyConfiguration : PollingProxyConfiguration, IWritablePollingProxyConfiguration
    {
        readonly IWritableKeyValueStore settings;

        public WritablePollingProxyConfiguration(IWritableKeyValueStore settings) : base(settings)
        {
            this.settings = settings;
        }

        public bool SetUseDefaultProxy(bool useDefaultProxy)
        {
            return settings.Set("Octopus.Server.Proxy.UseDefaultProxy", useDefaultProxy);
        }

        public bool SetCustomProxyUsername(string? username)
        {
            return settings.Set("Octopus.Server.Proxy.ProxyUsername", username);
        }

        public bool SetCustomProxyPassword(string? password)
        {
            return settings.Set("Octopus.Server.Proxy.ProxyPassword", password, ProtectionLevel.MachineKey);
        }

        public bool SetCustomProxyHost(string? host)
        {
            return settings.Set("Octopus.Server.Proxy.ProxyHost", host);
        }

        public bool SetCustomProxyPort(int port)
        {
            return settings.Set("Octopus.Server.Proxy.ProxyPort", port);
        }
    }
}