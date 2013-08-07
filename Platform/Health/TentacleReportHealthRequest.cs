using System;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;
using Pipefish;

namespace Octopus.Shared.Platform.Health
{
    [ExpectReply]
    public class TentacleReportHealthRequest : IMessage
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
