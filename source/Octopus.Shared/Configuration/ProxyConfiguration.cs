using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration
    {
        internal const string OctopusProxyUseDefault = "Octopus.Proxy.UseDefaultProxy";
        internal const string OctopusProxyHost = "Octopus.Proxy.ProxyHost";
        internal const string OctopusProxyPort = "Octopus.Proxy.ProxyPort";
        internal const string OctopusProxyUsername = "Octopus.Proxy.ProxyUsername";
        internal const string OctopusProxyPassword = "Octopus.Proxy.ProxyPassword";
        
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