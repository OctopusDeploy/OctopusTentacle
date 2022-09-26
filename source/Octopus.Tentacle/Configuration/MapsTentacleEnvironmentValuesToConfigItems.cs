#nullable enable
using System;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Configuration
{
    public class MapsTentacleEnvironmentValuesToConfigItems : MapsEnvironmentValuesToConfigItems
    {
        private static readonly string[] SupportedConfigurationKeys =
        {
            TentacleConfiguration.ServicesPortSettingName,
            TentacleConfiguration.ServicesListenIPSettingName,
            TentacleConfiguration.ServicesNoListenSettingName,
            TentacleConfiguration.TrustedServersSettingName,
            TentacleConfiguration.DeploymentApplicationDirectorySettingName,
            TentacleConfiguration.CertificateSettingName,
            TentacleConfiguration.CertificateThumbprintSettingName,
            PollingProxyConfiguration.UseDefaultProxySettingName,
            PollingProxyConfiguration.ProxyUsernameSettingName,
            PollingProxyConfiguration.ProxyPasswordSettingName,
            PollingProxyConfiguration.ProxyHostSettingName,
            PollingProxyConfiguration.ProxyPortSettingName
        };

        private static readonly EnvironmentVariable ServicesPort = EnvironmentVariable.PlaintText("TENTACLE_SERVICE_PORT");
        private static readonly EnvironmentVariable ListenIP = EnvironmentVariable.PlaintText("TENTACLE_LISTEN_IP");
        private static readonly EnvironmentVariable NoListen = EnvironmentVariable.PlaintText("TENTACLE_NO_LISTEN");
        private static readonly EnvironmentVariable TrustedServers = EnvironmentVariable.PlaintText("TENTACLE_TRUSTED_SERVERS");
        private static readonly EnvironmentVariable DeploymentApplicationDirectory = EnvironmentVariable.PlaintText("TENTACLE_APPLICATION_DIRECTORY");
        private static readonly EnvironmentVariable Certificate = EnvironmentVariable.PlaintText("TENTACLE_CERTIFICATE");
        private static readonly EnvironmentVariable CertificateThumbprint = EnvironmentVariable.PlaintText("TENTACLE_CERTIFICATE_THUMBPRINT");
        private static readonly EnvironmentVariable UseDefaultProxy = EnvironmentVariable.PlaintText("TENTACLE_POLLING_USE_DEFAULT_PROXY");
        private static readonly EnvironmentVariable ProxyUser = EnvironmentVariable.PlaintText("TENTACLE_POLLING_CUSTOM_PROXY_USER");
        private static readonly EnvironmentVariable ProxyPassword = EnvironmentVariable.Sensitive("TENTACLE_POLLING_CUSTOM_PROXY_PASSWORD", "polling proxy's password");
        internal static readonly EnvironmentVariable ProxyHost = EnvironmentVariable.PlaintText("TENTACLE_POLLING_CUSTOM_PROXY_HOST");
        private static readonly EnvironmentVariable ProxyPort = EnvironmentVariable.PlaintText("TENTACLE_POLLING_CUSTOM_PROXY_PORT");

        internal static readonly EnvironmentVariable[] SupportedEnvironmentValues =
        {
            ServicesPort,
            ListenIP,
            NoListen,
            TrustedServers,
            DeploymentApplicationDirectory,
            Certificate,
            CertificateThumbprint,
            UseDefaultProxy,
            ProxyUser,
            ProxyPassword,
            ProxyHost,
            ProxyPort
        };

        public MapsTentacleEnvironmentValuesToConfigItems(ILogFileOnlyLogger log) :
            base(log, SupportedConfigurationKeys, new EnvironmentVariable[0], SupportedEnvironmentValues)
        {
        }

        protected override string? MapConfigurationValue(string configurationSettingName)
        {
            switch (configurationSettingName)
            {
                case TentacleConfiguration.ServicesPortSettingName:
                    return EnvironmentValues[ServicesPort.Name];
                case TentacleConfiguration.ServicesListenIPSettingName:
                    return EnvironmentValues[ListenIP.Name];
                case TentacleConfiguration.ServicesNoListenSettingName:
                    return EnvironmentValues[NoListen.Name];
                case TentacleConfiguration.TrustedServersSettingName:
                    return EnvironmentValues[TrustedServers.Name];
                case TentacleConfiguration.DeploymentApplicationDirectorySettingName:
                    return EnvironmentValues[DeploymentApplicationDirectory.Name];
                case TentacleConfiguration.CertificateSettingName:
                    return EnvironmentValues[Certificate.Name];
                case TentacleConfiguration.CertificateThumbprintSettingName:
                    return EnvironmentValues[CertificateThumbprint.Name];

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
                case "Octopus.Communications.Squid":
                    return null;
            }

            throw new ArgumentException($"Unknown configuration setting {configurationSettingName}");
        }
    }
}