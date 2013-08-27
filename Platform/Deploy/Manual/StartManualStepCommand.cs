using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Manual
{
    public class StartManualStepCommand : StartDeploymentStepCommand
    {
        public StartManualStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId)
            : base(logger, deploymentId, deploymentStepId)
        {
        }
    }
}