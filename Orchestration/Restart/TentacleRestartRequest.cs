using System;
using Octopus.Shared.Communications.Conversations;
using Pipefish;

namespace Octopus.Shared.Orchestration.Restart
{
    [BeginsConversationEndedBy(typeof(TentacleRestartReply))]
    public class TentacleRestartRequest : IMessage
    {
    }
}
