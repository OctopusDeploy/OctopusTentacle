using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.TentaclePackageRetention
{
    public class PerformRetentionProcessingCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public List<RequiredRetentionProcessing> RetentionProcessing { get; private set; }

        public PerformRetentionProcessingCommand(LoggerReference logger, List<RequiredRetentionProcessing> retentionProcessing)
        {
            Logger = logger;
            RetentionProcessing = retentionProcessing;
        }
    }
}
