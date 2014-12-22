using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Deploy.Steps;

namespace Octopus.Shared.Messages.Deploy.Script
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
