using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;

namespace Octopus.Platform.Deployment.Messages.Deploy.Manual
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