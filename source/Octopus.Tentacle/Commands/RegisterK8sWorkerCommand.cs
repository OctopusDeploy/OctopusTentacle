using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class RegisterK8sWorkerCommand : RegisterMachineCommandBase<IRegisterWorkerOperation>
    {
        readonly List<string> workerpoolNames = new List<string>();

        public RegisterK8sWorkerCommand(Lazy<IRegisterWorkerOperation> lazyRegisterMachineOperation,
            Lazy<IWritableTentacleConfiguration> configuration,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            Lazy<IOctopusServerChecker> octopusServerChecker,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISpaceRepositoryFactory spaceRepositoryFactory,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(lazyRegisterMachineOperation, configuration, log, selector, octopusServerChecker, proxyConfig, octopusClientInitializer, spaceRepositoryFactory, logFileOnlyLogger)
        {
            Options.Add("workerpool=", "The worker pool name to add the machine to - e.g., 'Windows Pool'; specify this argument multiple times to add to multiple pools", s => workerpoolNames.Add(s));
        }

        protected override void CheckArgs()
        {
            if (workerpoolNames.Count == 0 || string.IsNullOrWhiteSpace(workerpoolNames.First()))
                throw new ControlledFailureException("Please specify a worker pool name, e.g., --workerpool=Default");
        }

        protected override void EnhanceOperation(IRegisterWorkerOperation registerOperation)
        {
            registerOperation.WorkerPoolNames = workerpoolNames.ToArray();
        }

        protected override void UpdateConfiguration(IWritableTentacleConfiguration writableConfiguration)
        {
            writableConfiguration.SetScriptExecutor(ScriptExecutor.KubernetesJob);
        }
    }
}