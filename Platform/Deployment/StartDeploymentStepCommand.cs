using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment
{
    public abstract class StartDeploymentStepCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentStepId { get; private set; }

        protected StartDeploymentStepCommand(LoggerReference logger, string deploymentStepId)
        {
            Logger = logger;
            DeploymentStepId = deploymentStepId;
        }
    }
}
