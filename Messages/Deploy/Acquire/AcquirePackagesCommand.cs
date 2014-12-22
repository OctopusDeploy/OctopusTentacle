using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Deploy.Acquire
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
