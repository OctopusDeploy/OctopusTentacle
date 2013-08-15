using System;
using Octopus.Shared.Platform.Deployment;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Email
{
    public class StartEmailStepCommand : StartDeploymentStepCommand
    {
        public StartEmailStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId) 
            : base(logger, deploymentId, deploymentStepId)
        {
        }
    }
}
