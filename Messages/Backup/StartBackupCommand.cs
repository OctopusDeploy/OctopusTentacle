using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Backup
{
    public class StartBackupCommand : ICorrelatedMessage
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