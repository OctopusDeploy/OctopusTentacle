using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Script
{
    public class StartScriptStepCommand : StartDeploymentStepCommand
    {
        public StartScriptStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId)
            : base(logger, deploymentId, deploymentStepId)
        {
        }
    }
}
