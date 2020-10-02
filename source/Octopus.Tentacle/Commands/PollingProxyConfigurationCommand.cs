using System;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

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