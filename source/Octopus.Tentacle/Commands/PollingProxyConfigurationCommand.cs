using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class PollingProxyConfigurationCommand : ProxyConfigurationCommand
    {
        public PollingProxyConfigurationCommand(Lazy<IWritablePollingProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector instanceSelector, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger)
            : base(new Lazy<IWritableProxyConfiguration>(() => proxyConfiguration.Value), instanceSelector, log, logFileOnlyLogger)
        {
        }
    }
}