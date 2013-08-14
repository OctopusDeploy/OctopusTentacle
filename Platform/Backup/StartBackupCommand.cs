using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Backup
{
    public class StartBackupCommand : IMessageWithLogger
    {
        readonly LoggerReference logger;

        public StartBackupCommand(LoggerReference logger)
        {
            this.logger = logger;
        }

        public LoggerReference Logger
        {
            get { return logger; }
        }
    }
}