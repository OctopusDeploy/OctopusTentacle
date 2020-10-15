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

        public bool UseDefaultProxy => settings.Get(ProxyUseDefaultSettingName, true);
        public string? CustomProxyUsername => settings.Get(ProxyUsernameSettingName, string.Empty);
        public string? CustomProxyPassword => settings.Get<string?>(ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);
        public string? CustomProxyHost => settings.Get(ProxyHostSettingName);
        public int CustomProxyPort => settings.Get(ProxyPortSettingName, 80);
    }

    public class WritableProxyConfiguration : IWritableProxyConfiguration
    {
        readonly IWritableKeyValueStore settings;

        public WritableProxyConfiguration(IWritableKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get => settings.Get(ProxyConfiguration.ProxyUseDefaultSettingName, true);
            set => settings.Set(ProxyConfiguration.ProxyUseDefaultSettingName, value);
        }

        public string? CustomProxyUsername
        {
            get => settings.Get(ProxyConfiguration.ProxyUsernameSettingName, string.Empty);
            set => settings.Set(ProxyConfiguration.ProxyUsernameSettingName, value);
        }

        public string? CustomProxyPassword
        {
            get => settings.Get<string?>(ProxyConfiguration.ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);
            set => settings.Set(ProxyConfiguration.ProxyPasswordSettingName, value);
        }

        public string? CustomProxyHost
        {
            get => settings.Get(ProxyConfiguration.ProxyHostSettingName);
            set => settings.Set(ProxyConfiguration.ProxyHostSettingName, value);
        }

        public int CustomProxyPort
        {
            get => settings.Get(ProxyConfiguration.ProxyPortSettingName, 80);
            set => settings.Set(ProxyConfiguration.ProxyPortSettingName, value);
        }
    }
}