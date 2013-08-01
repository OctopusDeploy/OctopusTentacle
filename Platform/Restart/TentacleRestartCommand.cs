using System;
using Octopus.Shared.Communications.Conversations;
using Pipefish;

namespace Octopus.Shared.Orchestration.Restart
{
    [BeginsConversationEndedBy(typeof(TentacleRestartResult))]
    public class TentacleRestartCommand : IMessage
    {
    }
}
