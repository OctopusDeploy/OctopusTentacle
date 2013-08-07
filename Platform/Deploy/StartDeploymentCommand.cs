using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deployment
{
    public class StartDeploymentCommand : IMessageWithLogger
    {
        public string TaskId { get; private set; }
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; private set; }

        public StartDeploymentCommand(string taskId, LoggerReference logger, string deploymentId)
        {
            TaskId = taskId;
            Logger = logger;
            DeploymentId = deploymentId;
        }
    }
}
