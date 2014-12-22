using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public abstract class StartDeploymentActionCommand : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }
        public abstract IReusableMessage CopyForReuse(LoggerReference newLogger);

        public string DeploymentId { get; set; }
        public string DeploymentActionId { get; private set; }

        protected StartDeploymentActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId)
        {
            Logger = logger;
            DeploymentId = deploymentId;
            DeploymentActionId = deploymentActionId;
        }
    }
}
