using System;
using System.Collections.Generic;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class ProxyConfigurationCommand : AbstractStandardCommand
    {
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly List<Action> operations = new List<Action>();

        public ProxyConfigurationCommand(Lazy<IProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ILog log) : base(instanceSelector)
        {
            this.proxyConfiguration = proxyConfiguration;

            Options.Add("proxyEnable=", "Whether to use a proxy", v => QueueOperation(delegate
            {
                var useDefaultProxy = bool.Parse(v);
                proxyConfiguration.Value.UseDefaultProxy = useDefaultProxy;
                log.Info("Use a proxy: " + v);
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
                proxyConfiguration.Value.CustomProxyHost = v;
                log.Info(string.IsNullOrWhiteSpace(v) ? "Using Internet Explorer Proxy" : "Proxy host set to: " + v);
            }));

            Options.Add("proxyPort=", "The proxy port to use in conjuction with the Host set with proxyHost", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyPort = string.IsNullOrWhiteSpace(v) ? 80 : int.Parse(v);
                log.Info("Proxy port set to: " + proxyConfiguration.Value.CustomProxyPort + ". Value only used if proxyHost also set.");
            }));
        }

        protected override void Start()
        {
            base.Start();

            foreach (var operation in operations) operation();

            proxyConfiguration.Value.Save();
        }

        void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}