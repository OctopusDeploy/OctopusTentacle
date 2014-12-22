using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Retention
{
    public class ApplyRetentionPolicyCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string LifecycleId { get; private set; }
        public IList<string> ProjectIds { get; private set; }

        public ApplyRetentionPolicyCommand(LoggerReference logger, string lifecycleId, IList<string> projectIds)
        {
            Logger = logger;
            LifecycleId = lifecycleId;
            ProjectIds = projectIds;
        }
    }
}
