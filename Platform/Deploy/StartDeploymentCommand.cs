using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy
{
    public class StartDeploymentCommand : IMessageWithLogger
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
