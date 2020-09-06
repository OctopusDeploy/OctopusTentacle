using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration
    {
        // These are deliberately public so consumers like Octopus Server and Tentacle can use the configuration keys
        public const string OctopusProxyUseDefault = "Octopus.Proxy.UseDefaultProxy";
        public const string OctopusProxyHost = "Octopus.Proxy.ProxyHost";
        public const string OctopusProxyPort = "Octopus.Proxy.ProxyPort";
        public const string OctopusProxyUsername = "Octopus.Proxy.ProxyUsername";
        public const string OctopusProxyPassword = "Octopus.Proxy.ProxyPassword";
        
        readonly IKeyValueStore settings;

        public ProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get { return settings.Get<bool>(OctopusProxyUseDefault, true); }
            set { settings.Set(OctopusProxyUseDefault, value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get(OctopusProxyUsername, string.Empty); }
            set { settings.Set(OctopusProxyUsername, value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get<string>(OctopusProxyPassword, protectionLevel: ProtectionLevel.MachineKey); }
            set { settings.Set(OctopusProxyPassword, value, ProtectionLevel.MachineKey); }
        }

        public string? CustomProxyHost
        {
            get { return settings.Get(OctopusProxyHost); }
            set { settings.Set(OctopusProxyHost, value); }
        }

        public int CustomProxyPort
        {
            get { return settings.Get(OctopusProxyPort, 80); }
            set { settings.Set(OctopusProxyPort, value); }
        }
    }
}
