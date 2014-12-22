using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.Health
{
    [ExpectReply]
    public class TentacleReportHealthRequest : ICorrelatedMessage
    {
        readonly LoggerReference logger;

        public TentacleReportHealthRequest(LoggerReference logger)
        {
            this.logger = logger;
        }

        public LoggerReference Logger
        {
            get { return logger; }
        }
    }
}
