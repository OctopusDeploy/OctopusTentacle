using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment.Package
{
    public class StartPackageStepCommand : StartDeploymentStepCommand
    {
        public StartPackageStepCommand(LoggerReference logger, string deploymentStepId) 
            : base(logger, deploymentStepId)
        {
        }
    }
}
