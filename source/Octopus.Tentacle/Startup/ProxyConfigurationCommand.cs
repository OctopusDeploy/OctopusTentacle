using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Tentacle.Startup
{
    public class ProxyConfigurationCommand : AbstractStandardCommand
    {
        private readonly Lazy<IWritableProxyConfiguration> proxyConfiguration;
        private readonly List<Action> operations = new();
        private bool useAProxy;
        private string? host;

        public ProxyConfigurationCommand(Lazy<IWritableProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ISystemLog systemLog, ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, systemLog, logFileOnlyLogger)
        {
            this.proxyConfiguration = proxyConfiguration;

            Options.Add("proxyEnable=",
                "Whether to use a proxy",
                v => QueueOperation(delegate
                {
                    useAProxy = bool.Parse(v);
                }));

            Options.Add("proxyUsername=",
                "Username to use when authenticating with the proxy",
                v => QueueOperation(delegate
                {
                    proxyConfiguration.Value.SetCustomProxyUsername(v);
                    systemLog.Info(string.IsNullOrWhiteSpace(v) ? "Proxy username cleared" : "Proxy username set to: " + v);
                }));

            Options.Add("proxyPassword=",
                "Password to use when authenticating with the proxy",
                v => QueueOperation(delegate
                {
                    proxyConfiguration.Value.SetCustomProxyPassword(v);
                    systemLog.Info(string.IsNullOrWhiteSpace(v) ? "Proxy password cleared" : "Proxy password set to: *******");
                }),
                sensitive: true);

            Options.Add("proxyHost=",
                "The proxy host to use. Leave empty to use the default Internet Explorer proxy",
                v => QueueOperation(delegate
                {
                    if (!string.IsNullOrWhiteSpace(v))
                        host = new UriBuilder(v).Host;
                }));

            Options.Add("proxyPort=",
                "The proxy port to use in conjunction with the Host set with proxyHost",
                v => QueueOperation(delegate
                {
                    proxyConfiguration.Value.SetCustomProxyPort(string.IsNullOrWhiteSpace(v) ? 80 : int.Parse(v));
                    if (host != null)
                        systemLog.Info("Proxy port set to: " + proxyConfiguration.Value.CustomProxyPort);
                }));
        }

        protected override void Start()
        {
            base.Start();

            foreach (var operation in operations) operation();

            if (useAProxy)
            {
                proxyConfiguration.Value.SetCustomProxyHost(host);
                proxyConfiguration.Value.SetUseDefaultProxy(host == null);
                SystemLog.Info(host != null
                    ? "Using custom proxy at: " + host
                    : "Using Internet Explorer Proxy");
            }
            else
            {
                proxyConfiguration.Value.SetCustomProxyHost(null);
                proxyConfiguration.Value.SetUseDefaultProxy(false);
                SystemLog.Info("Proxy use is disabled");
            }
        }

        private void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}