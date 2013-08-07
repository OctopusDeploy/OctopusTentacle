using System;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Restart
{
    [ExpectReply]
    public class TentacleRestartCommand : IMessageWithLogger
    {
        public TentacleRestartCommand(LoggerReference logger)
        {
            Logger = logger;
        }

        public LoggerReference Logger { get; private set; }
    }
}
