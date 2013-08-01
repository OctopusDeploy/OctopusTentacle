using System;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Platform.Restart
{
    [BeginsConversationEndedBy(typeof(TentacleRestartResult))]
    public class TentacleRestartCommand : IMessage
    {
    }
}
