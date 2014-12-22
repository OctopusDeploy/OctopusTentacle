using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.TestEmail
{
    public class StartEmailTestCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string SendToEmail { get; private set; }

        public StartEmailTestCommand(LoggerReference logger, string sendToEmail)
        {
            Logger = logger;
            SendToEmail = sendToEmail;
        }
    }
}
