using System;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Platform.Restart
{
    [BeginsConversationEndedBy(typeof(TentacleRestartedEvent), typeof(CompletionEvent))]
    public class TentacleRestartCommand : IMessageWithLogger
    {
        public TentacleRestartCommand(LoggerReference logger)
        {
            Logger = logger;
        }

        public LoggerReference Logger { get; private set; }
    }
}
