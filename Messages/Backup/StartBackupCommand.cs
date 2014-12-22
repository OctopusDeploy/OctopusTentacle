using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Backup
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