using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration
    {
        // These are deliberately public so consumers like Octopus Server and Tentacle can use the configuration keys
        public const string ProxyUseDefaultSettingName = "Octopus.Proxy.UseDefaultProxy";
        public const string ProxyHostSettingName = "Octopus.Proxy.ProxyHost";
        public const string ProxyPortSettingName = "Octopus.Proxy.ProxyPort";
        public const string ProxyUsernameSettingName = "Octopus.Proxy.ProxyUsername";
        public const string ProxyPasswordSettingName = "Octopus.Proxy.ProxyPassword";
        
        readonly IKeyValueStore settings;

        public ProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get => settings.Get<bool>(ProxyUseDefaultSettingName, true);
            set => settings.Set(ProxyUseDefaultSettingName, value);
        }

        public string CustomProxyUsername
        {
            get => settings.Get(ProxyUsernameSettingName, string.Empty);
            set => settings.Set(ProxyUsernameSettingName, value);
        }

        public string CustomProxyPassword
        {
            get => settings.Get<string>(ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);
            set => settings.Set(ProxyPasswordSettingName, value, ProtectionLevel.MachineKey);
        }

        public string? CustomProxyHost
        {
            get => settings.Get(ProxyHostSettingName);
            set => settings.Set(ProxyHostSettingName, value);
        }

        public int CustomProxyPort
        {
            get => settings.Get(ProxyPortSettingName, 80);
            set => settings.Set(ProxyPortSettingName, value);
        }
    }
}
