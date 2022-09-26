#nullable enable
using System;
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration
{
    internal class PollingProxyConfiguration : IPollingProxyConfiguration
    {
        public const string UseDefaultProxySettingName = "Octopus.Server.Proxy.UseDefaultProxy";
        public const string ProxyUsernameSettingName = "Octopus.Server.Proxy.ProxyUsername";
        public const string ProxyPasswordSettingName = "Octopus.Server.Proxy.ProxyPassword";
        public const string ProxyHostSettingName = "Octopus.Server.Proxy.ProxyHost";
        public const string ProxyPortSettingName = "Octopus.Server.Proxy.ProxyPort";

        private readonly IKeyValueStore settings;

        public PollingProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy => settings.Get(UseDefaultProxySettingName, true);

        public string? CustomProxyUsername => settings.Get(ProxyUsernameSettingName, string.Empty);

        public string? CustomProxyPassword => settings.Get<string>(ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);

        public string? CustomProxyHost => settings.Get(ProxyHostSettingName, string.Empty);

        public int CustomProxyPort => settings.Get(ProxyPortSettingName, 80);
    }

    internal class WritablePollingProxyConfiguration : PollingProxyConfiguration, IWritablePollingProxyConfiguration
    {
        private readonly IWritableKeyValueStore settings;

        public WritablePollingProxyConfiguration(IWritableKeyValueStore settings) : base(settings)
        {
            this.settings = settings;
        }

        public bool SetUseDefaultProxy(bool useDefaultProxy)
        {
            return settings.Set(UseDefaultProxySettingName, useDefaultProxy);
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