using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Deploy.Steps;
using Octopus.Shared.Packages;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Messages.Deploy.Package
{
    public class StartDeployPackageActionCommand : StartTentacleDeploymentActionCommand
    {
        public PackageMetadata Package { get; private set; }
        public VariableDictionary SpecialVariables { get; private set; }

        public StartDeployPackageActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId, PackageMetadata package, string machineId, VariableDictionary specialVariables) 
            : base(logger, deploymentId, deploymentActionId, machineId)
        {
            Package = package;
            SpecialVariables = new VariableDictionary((specialVariables ?? new VariableDictionary()));
        }

        public override IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new StartDeployPackageActionCommand(newLogger, DeploymentId, DeploymentActionId, Package, MachineId, SpecialVariables);
        }
    }
}
