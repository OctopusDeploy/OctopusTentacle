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
        readonly ILog log;

        public ProxyConfigurationCommand(Lazy<IProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ILog log) : base(instanceSelector)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.log = log;

            Options.Add("proxyEnable=", "Whether to use the Internet Explorer proxy", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.UseDefaultProxy = bool.Parse(v);
                log.Info("Use IE proxy: " + v);
            }));
            Options.Add("proxyUsername=", "Username to use when authenticating with the proxy", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyUsername = v;
                log.Info("Proxy username: " + v);
            }));
            Options.Add("proxyPassword=", "Password to use when authenticating with the proxy", v => QueueOperation(delegate
            {
                proxyConfiguration.Value.CustomProxyPassword = v;
                if (string.IsNullOrWhiteSpace(v))
                {
                    log.Info("No proxy password used");
                }
                else
                {
                    log.Info("Proxy password set");
                }
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