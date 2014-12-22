using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;

namespace Octopus.Platform.Deployment.Messages.Deploy.Email
{
    public class StartEmailActionCommand : StartDeploymentActionCommand
    {
        public StartEmailActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId) 
            : base(logger, deploymentId, deploymentActionId)
        {
        }

        public override IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new StartEmailActionCommand(newLogger, DeploymentId, DeploymentActionId);
        }
    }
}
