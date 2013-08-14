using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Retention
{
    public class StartRetentionPolicyApplicationCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }

        public StartRetentionPolicyApplicationCommand(LoggerReference logger)
        {
            Logger = logger;
        }
    }
}
