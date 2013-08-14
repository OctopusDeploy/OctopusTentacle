using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.TestEmail
{
    public class StartEmailTestCommand : IMessageWithLogger
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
