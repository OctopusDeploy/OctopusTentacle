using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Deploy
{
    public class StartDeploymentCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; private set; }

        public StartDeploymentCommand(LoggerReference logger, string deploymentId)
        {
            Logger = logger;
            DeploymentId = deploymentId;
        }
    }
}
