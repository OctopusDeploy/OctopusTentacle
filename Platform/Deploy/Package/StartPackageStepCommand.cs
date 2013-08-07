using System;
using Octopus.Shared.Platform.Deployment;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Package
{
    public class StartPackageStepCommand : StartDeploymentStepCommand
    {
        public StartPackageStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId) 
            : base(logger, deploymentId, deploymentStepId)
        {
        }
    }
}
