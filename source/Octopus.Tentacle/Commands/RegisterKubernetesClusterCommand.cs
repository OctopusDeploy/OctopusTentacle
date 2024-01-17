using System;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using YamlDotNet.Serialization;

namespace Octopus.Tentacle.Commands
{
    public class RegisterKubernetesClusterCommand : RegisterMachineCommand<IRegisterKubernetesClusterOperation>
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;

        public RegisterKubernetesClusterCommand(Lazy<IRegisterKubernetesClusterOperation> lazyRegisterMachineOperation, Lazy<IWritableTentacleConfiguration> configuration, ISystemLog log, IApplicationInstanceSelector selector, Lazy<IOctopusServerChecker> octopusServerChecker, IProxyConfigParser proxyConfig, IOctopusClientInitializer octopusClientInitializer, ISpaceRepositoryFactory spaceRepositoryFactory, ILogFileOnlyLogger logFileOnlyLogger) : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
            this.configuration = configuration;
        }

        protected override void Start()
        {
            if (configuration.Value.IsRegistered) return;

            base.Start();
            configuration.Value.SetIsRegistered();
        }
    }
}