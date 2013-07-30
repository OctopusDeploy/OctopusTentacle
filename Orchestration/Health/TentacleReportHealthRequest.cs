using System;
using Octopus.Shared.Communications.Conversations;
using Octopus.Shared.Communications.Logging;
using Pipefish.Standard;

namespace Octopus.Shared.Orchestration.Health
{
    [BeginsConversationEndedBy(typeof(TentacleReportHealthReply))]
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
