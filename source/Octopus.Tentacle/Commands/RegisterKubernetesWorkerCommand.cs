using System;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class RegisterKubernetesWorkerCommand : RegisterWorkerCommand<IRegisterKubernetesWorkerOperation>
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly ISystemLog log;

        public RegisterKubernetesWorkerCommand(Lazy<IRegisterKubernetesWorkerOperation> lazyRegisterMachineOperation, Lazy<IWritableTentacleConfiguration> configuration, ISystemLog log, IApplicationInstanceSelector selector, Lazy<IOctopusServerChecker> octopusServerChecker, IProxyConfigParser proxyConfig, IOctopusClientInitializer octopusClientInitializer, ISpaceRepositoryFactory spaceRepositoryFactory, ILogFileOnlyLogger logFileOnlyLogger) : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;
        }
        
        protected override void Start()
        {
            if (configuration.Value.IsRegistered)
            {
                log.Info("Tentacle is already registered, skipping registration.");
                return;
            }

            base.Start();
            configuration.Value.SetIsRegistered();
            log.Info("Tentacle has been registered successfully.");
        }
    }
}