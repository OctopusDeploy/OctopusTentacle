using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.TentaclePackageRetention
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
