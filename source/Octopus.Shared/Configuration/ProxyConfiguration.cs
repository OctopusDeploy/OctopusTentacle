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

        public bool UseDefaultProxy => settings.Get<bool>(ProxyUseDefaultSettingName, true);

        public string CustomProxyUsername => settings.Get(ProxyUsernameSettingName, string.Empty);

        public string CustomProxyPassword => settings.Get<string>(ProxyPasswordSettingName, protectionLevel: ProtectionLevel.MachineKey);

        public string? CustomProxyHost => settings.Get(ProxyHostSettingName);

        public int CustomProxyPort => settings.Get(ProxyPortSettingName, 80);
    }

    public class ModifiableProxyConfiguration : ProxyConfiguration, IModifiableProxyConfiguration
    {
        readonly IModifiableKeyValueStore settings;

        public ModifiableProxyConfiguration(IModifiableKeyValueStore settings) : base(settings)
        {
            this.settings = settings;
        }

        public void SetUseDefaultProxy(bool useDefaultProxy)
        {
            settings.Set(ProxyUseDefaultSettingName, useDefaultProxy);
        }

        public void SetCustomProxyUsername(string? username)
        {
            settings.Set(ProxyUsernameSettingName, username);
        }

        public void SetCustomProxyPassword(string? password)
        {
            settings.Set(ProxyPasswordSettingName, password, ProtectionLevel.MachineKey);
        }

        public void SetCustomProxyHost(string? host)
        {
            settings.Set(ProxyHostSettingName, host);;
        }

        public void SetCustomProxyPort(int? port)
        {
            settings.Set(ProxyPortSettingName, port);
        }
    }
}