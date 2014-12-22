using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.Restart
{
    [ExpectReply]
    public class TentacleRestartCommand : ICorrelatedMessage
    {
        public TentacleRestartCommand(LoggerReference logger)
        {
            Logger = logger;
        }

        public LoggerReference Logger { get; private set; }
    }
}
