using System;
using Octopus.Client.Operations;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    [Obsolete("This class is being deprecated in favour of RegisterKubernetesDeploymentTargetCommand. To be removed in 2025.1")]
    public class RegisterKubernetesClusterCommand : RegisterMachineCommand<IRegisterKubernetesClusterOperation>
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly ISystemLog log;
        string? defaultNamespace;

        public RegisterKubernetesClusterCommand(Lazy<IRegisterKubernetesClusterOperation> lazyRegisterMachineOperation, Lazy<IWritableTentacleConfiguration> configuration, ISystemLog log, IApplicationInstanceSelector selector, Lazy<IOctopusServerChecker> octopusServerChecker, IProxyConfigParser proxyConfig, IOctopusClientInitializer octopusClientInitializer, ISpaceRepositoryFactory spaceRepositoryFactory, ILogFileOnlyLogger logFileOnlyLogger) : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.log = log;

            Options.Add("default-namespace=", "The default namespace by kubernetes steps if no namespace is supplied (if unset, defaults to 'default')", n => defaultNamespace = n);
        }

        protected override void Start()
        {
            log.Warn("This command is being deprecated. Please use the \"register-k8s-target\" command instead.");
            
            if (configuration.Value.IsRegistered)
            {
                log.Info("Tentacle is already registered, skipping registration.");
                return;
            }

            base.Start();
            configuration.Value.SetIsRegistered();
            log.Info("Tentacle has been registered successfully.");
        }

        protected override void EnhanceOperation(IRegisterKubernetesClusterOperation registerOperation)
        {
            base.EnhanceOperation(registerOperation);

            registerOperation.DefaultNamespace = defaultNamespace;
        }
    }
}