#nullable enable
using System;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Configuration
{
    public class MapsTentacleEnvironmentVariablesToConfigItems : MapsEnvironmentValuesToConfigItems
    {
        static readonly string[] SupportedConfigurationKeys =
            {
                PollingProxyConfiguration.UseDefaultProxySettingName,
                PollingProxyConfiguration.ProxyUsernameSettingName,
                PollingProxyConfiguration.ProxyPasswordSettingName,
                PollingProxyConfiguration.ProxyHostSettingName,
                PollingProxyConfiguration.ProxyPortSettingName
            };

        static readonly EnvironmentVariable UseDefaultProxy = EnvironmentVariable.PlaintText("OCTOPUS_POLLING_USE_DEFAULT_PROXY");
        static readonly EnvironmentVariable ProxyUser = EnvironmentVariable.PlaintText("OCTOPUS_POLLING_CUSTOM_PROXY_USER");
        static readonly EnvironmentVariable ProxyPassword = EnvironmentVariable.Sensitive("OCTOPUS_POLLING_CUSTOM_PROXY_PASSWORD", "polling proxy's password");
        static readonly EnvironmentVariable ProxyHost = EnvironmentVariable.PlaintText("OCTOPUS_POLLING_CUSTOM_PROXY_HOST");
        static readonly EnvironmentVariable ProxyPort = EnvironmentVariable.PlaintText("OCTOPUS_POLLING_CUSTOM_PROXY_PORT");

        static readonly EnvironmentVariable[] OptionalEnvironmentVariables =
        {
            UseDefaultProxy,
            ProxyUser,
            ProxyPassword,
            ProxyHost,
            ProxyPort
        };

        public MapsTentacleEnvironmentVariablesToConfigItems(ILogFileOnlyLogger log) :
            base(log, SupportedConfigurationKeys, new EnvironmentVariable[0], OptionalEnvironmentVariables)
        {
        }

        protected override string? MapConfigurationValue(string configurationSettingName)
        {
            switch (configurationSettingName)
            {
                case PollingProxyConfiguration.UseDefaultProxySettingName:
                    return EnvironmentValues[UseDefaultProxy.Name];
                case PollingProxyConfiguration.ProxyUsernameSettingName:
                    return EnvironmentValues[ProxyUser.Name];
                case PollingProxyConfiguration.ProxyPasswordSettingName:
                    return EnvironmentValues[ProxyPassword.Name];
                case PollingProxyConfiguration.ProxyHostSettingName:
                    return EnvironmentValues[ProxyHost.Name];
                case PollingProxyConfiguration.ProxyPortSettingName:
                    return EnvironmentValues[ProxyPort.Name];
            }

            throw new ArgumentException($"Unknown configuration setting {configurationSettingName}");
        }
    }
}