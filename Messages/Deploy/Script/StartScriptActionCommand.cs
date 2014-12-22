using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;

namespace Octopus.Platform.Deployment.Messages.Deploy.Script
{
    public class StartScriptActionCommand : StartTentacleDeploymentActionCommand
    {
        public StartScriptActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId, string machineId)
            : base(logger, deploymentId, deploymentActionId, machineId)
        {
        }

        public override IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new StartScriptActionCommand(newLogger, DeploymentId, DeploymentActionId, MachineId);
        }
    }
}
