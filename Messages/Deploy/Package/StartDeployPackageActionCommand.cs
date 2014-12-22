using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;
using Octopus.Platform.Deployment.Packages;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.Deploy.Package
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
