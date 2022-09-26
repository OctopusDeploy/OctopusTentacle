using System;
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration
    {
        // These are deliberately public so consumers like Octopus Server and Tentacle can use the configuration keys
        public const string ProxyUseDefaultSettingName = "Octopus.Proxy.UseDefaultProxy";
        public const string ProxyHostSettingName = "Octopus.Proxy.ProxyHost";
        public const string ProxyPortSettingName = "Octopus.Proxy.ProxyPort";
        public const string ProxyUsernameSettingName = "Octopus.Proxy.ProxyUsername";
        public const string ProxyPasswordSettingName = "Octopus.Proxy.ProxyPassword";

        private readonly IKeyValueStore settings;

        public ProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy => settings.Get(ProxyUseDefaultSettingName, true)!;
        public string? CustomProxyUsername => settings.Get(ProxyUsernameSettingName, (string?)null);
        public string? CustomProxyPassword => settings.Get<string?>(ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);
        public string? CustomProxyHost => settings.Get<string?>(ProxyHostSettingName);
        public int CustomProxyPort => settings.Get(ProxyPortSettingName, 80)!;
    }

    public class WritableProxyConfiguration : ProxyConfiguration, IWritableProxyConfiguration
    {
        private readonly IWritableKeyValueStore settings;

        public WritableProxyConfiguration(IWritableKeyValueStore settings) : base(settings)
        {
            this.settings = settings;
        }

        public bool SetUseDefaultProxy(bool useDefaultProxy)
        {
            return settings.Set(ProxyUseDefaultSettingName, useDefaultProxy);
        }

        public bool SetCustomProxyUsername(string? username)
        {
            return settings.Set(ProxyUsernameSettingName, username);
        }

        public bool SetCustomProxyPassword(string? password)
        {
            return settings.Set(ProxyPasswordSettingName, password, ProtectionLevel.MachineKey);
        }

        public bool SetCustomProxyHost(string? host)
        {
            return settings.Set(ProxyHostSettingName, host);
        }

        public bool SetCustomProxyPort(int port)
        {
            return settings.Set(ProxyPortSettingName, port);
        }
    }
}