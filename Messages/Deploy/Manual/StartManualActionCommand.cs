using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Deploy.Steps;

namespace Octopus.Shared.Messages.Deploy.Manual
{
    public class StartManualActionCommand : StartDeploymentActionCommand
    {
        public StartManualActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId)
            : base(logger, deploymentId, deploymentActionId)
        {
        }

        public override IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new StartManualActionCommand(newLogger, DeploymentId, DeploymentActionId);
        }
    }
}