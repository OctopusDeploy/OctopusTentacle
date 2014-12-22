using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Deploy.Steps;

namespace Octopus.Shared.Messages.Deploy.Email
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
