using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment.Script
{

    public class StartScriptStepCommand : StartDeploymentStepCommand
    {
        public StartScriptStepCommand(LoggerReference logger, string deploymentStepId)
            : base(logger, deploymentStepId)
        {
        }
    }
}
