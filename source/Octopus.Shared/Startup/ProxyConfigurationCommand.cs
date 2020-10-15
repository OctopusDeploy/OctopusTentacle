using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Startup
{
    public class ProxyConfigurationCommand : AbstractStandardCommand
    {
        readonly List<Action> operations = new List<Action>();
        readonly Lazy<IWritableProxyConfiguration> proxyConfiguration;
        readonly ILog log;
        bool useAProxy;
        string? host;

        public ProxyConfigurationCommand(IApplicationInstanceSelector instanceSelector, ILog log, Lazy<IWritableProxyConfiguration> proxyConfiguration) : base(instanceSelector)
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
            }), sensitive: true);

            Options.Add("proxyHost=", "The proxy host to use. Leave empty to use the default Internet Explorer proxy", v => QueueOperation(delegate
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    host = new UriBuilder(v).Host;
                }
            }));

            Options.Add("proxyPort=", "The proxy port to use in conjunction with the Host set with proxyHost", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyPort = string.IsNullOrWhiteSpace(v) ? 80 : int.Parse(v);
                if (host != null)
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
                proxyConfiguration.Value.CustomProxyHost = host;
                proxyConfiguration.Value.UseDefaultProxy = host == null;
                log.Info(host != null
                    ? "Using custom proxy at: " + host
                    : "Using Internet Explorer Proxy");
            }
            else
            {
                proxyConfiguration.Value.CustomProxyHost = null;
                proxyConfiguration.Value.UseDefaultProxy = false;
                log.Info("Proxy use is disabled");
            }
        }

        void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}