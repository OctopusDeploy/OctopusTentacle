using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Retention
{
    public class StartRetentionPolicyApplicationCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }

        public StartRetentionPolicyApplicationCommand(LoggerReference logger)
        {
            Logger = logger;
        }
    }
}
