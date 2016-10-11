using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class PollingProxyConfigurationCommand : ProxyConfigurationCommand
    {
        public PollingProxyConfigurationCommand(Lazy<IPollingProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ILog log)
            : base(new Lazy<IProxyConfiguration>(() => proxyConfiguration.Value), instanceSelector, log)
        {
        }
    }

    public class ProxyConfigurationCommand : AbstractStandardCommand
    {
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly List<Action> operations = new List<Action>();
        readonly ILog log;
        bool hostSet;
        bool useAProxy;
        string host;

        public ProxyConfigurationCommand(Lazy<IProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ILog log) : base(instanceSelector)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.log = log;

            Options.Add("proxyEnable=", "Whether to use a proxy", v => QueueOperation(delegate
            {
                useAProxy = bool.Parse(v);
            }));

            Options.Add("proxyUsername=", "Username to use when authenticating with the proxy", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyUsername = v;
                log.Info(string.IsNullOrWhiteSpace(v) ? "Proxy username cleared" : "Proxy username set to: " + v);
            }));

            Options.Add("proxyPassword=", "Password to use when authenticating with the proxy", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyPassword = v;
                log.Info(string.IsNullOrWhiteSpace(v) ? "Proxy password cleared" : "Proxy password set to: *******");
            }));

            Options.Add("proxyHost=", "The proxy host to use. Leave empty to use the default Internet Explorer proxy", v => QueueOperation(delegate
            {
                hostSet = !string.IsNullOrWhiteSpace(v);
                if (hostSet)
                {
                    host = new UriBuilder(v).Host;
                }
            }));

            Options.Add("proxyPort=", "The proxy port to use in conjuction with the Host set with proxyHost", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyPort = string.IsNullOrWhiteSpace(v) ? 80 : int.Parse(v);
                if (hostSet)
                {
                    log.Info("Proxy port set to: " + proxyConfiguration.Value.CustomProxyPort);
                }
            }));
        }

        protected override void Start()
        {
            base.Start();

            foreach (var operation in operations) operation();

            if (useAProxy)
            {
                proxyConfiguration.Value.CustomProxyHost = hostSet ? host : null;
                proxyConfiguration.Value.UseDefaultProxy = !hostSet;
                log.Info(hostSet 
                    ? "Using custom proxy at: " + host
                    : "Using Internet Explorer Proxy");
            }
            else
            {
                proxyConfiguration.Value.CustomProxyHost = null;
                proxyConfiguration.Value.UseDefaultProxy = false;
                log.Info("Proxy use is disabled");
            }

            proxyConfiguration.Value.Save();
        }

        void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}