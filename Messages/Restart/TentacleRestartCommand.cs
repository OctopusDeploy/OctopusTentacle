using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;

namespace Octopus.Platform.Deployment.Messages.Restart
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
