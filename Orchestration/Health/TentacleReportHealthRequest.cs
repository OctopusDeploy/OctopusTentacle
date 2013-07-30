using System;
using Octopus.Shared.Communications.Conversations;
using Octopus.Shared.Communications.Logging;
using Pipefish.Standard;

namespace Octopus.Shared.Orchestration.Health
{
    [BeginsConversationEndedBy(typeof(TentacleReportHealthReply))]
    public class TentacleReportHealthRequest : IMessage
    {
        public ActivityLogContext Logger { get; set; }

        public TentacleReportHealthRequest(ActivityLogContext logger)
        {
            Logger = logger;
        }
    }
}
