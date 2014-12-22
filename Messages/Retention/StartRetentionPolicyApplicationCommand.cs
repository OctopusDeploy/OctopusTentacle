using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Retention
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
