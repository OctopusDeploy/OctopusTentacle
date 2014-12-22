using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;

namespace Octopus.Platform.Deployment.Messages.TentaclePackageRetention
{
    [ExpectReply]
    public class TentaclePerformRetentionProcessingCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public List<string> RetentionTokens { get; private set; }

        public TentaclePerformRetentionProcessingCommand(LoggerReference logger, List<string> retentionTokens)
        {
            Logger = logger;
            RetentionTokens = retentionTokens;
        }
    }
}
