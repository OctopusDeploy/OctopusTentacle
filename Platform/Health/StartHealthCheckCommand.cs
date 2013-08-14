using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Health
{
    public class StartHealthCheckCommand : IMessageWithLogger
    {
        readonly LoggerReference logger;

        public StartHealthCheckCommand(LoggerReference logger)
        {
            this.logger = logger;
        }

        public LoggerReference Logger
        {
            get { return logger; }
        }
    }
}