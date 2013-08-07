using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Acquire
{
    public class AcquirePackagesCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; set; }

        public AcquirePackagesCommand(LoggerReference logger, string deploymentId)
        {
            Logger = logger;
            DeploymentId = deploymentId;
        }
    }
}
