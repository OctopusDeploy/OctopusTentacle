using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Retention
{
    public class ApplyRetentionPolicyCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string RetentionPolicyId { get; private set; }
        public IList<string> ProjectIds { get; private set; }

        public ApplyRetentionPolicyCommand(LoggerReference logger, string retentionPolicyId, IList<string> projectIds)
        {
            Logger = logger;
            RetentionPolicyId = retentionPolicyId;
            ProjectIds = projectIds;
        }
    }
}
