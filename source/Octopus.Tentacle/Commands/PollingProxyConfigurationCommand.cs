using System;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using IPollingProxyConfiguration = Octopus.Tentacle.Configuration.IPollingProxyConfiguration;

namespace Octopus.Tentacle.Commands
{
    public class PollingProxyConfigurationCommand : ProxyConfigurationCommand
    {
        public PollingProxyConfigurationCommand(Lazy<IPollingProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ILog log)
            : base(new Lazy<IProxyConfiguration>(() => proxyConfiguration.Value), instanceSelector, log)
        {
        }
    }
}
