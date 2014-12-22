using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;

namespace Octopus.Platform.Deployment.Messages.Health
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
