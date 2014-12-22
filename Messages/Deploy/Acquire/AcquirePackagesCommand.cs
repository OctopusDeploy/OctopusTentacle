using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Deploy.Acquire
{
    public class AcquirePackagesCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string DeploymentId { get; private set; }
        public List<string> IncludedActionIds { get; private set; }

        public AcquirePackagesCommand(LoggerReference logger, string deploymentId, List<string> includedActionIds)
        {
            Logger = logger;
            DeploymentId = deploymentId;
            IncludedActionIds = includedActionIds;
        }
    }
}
