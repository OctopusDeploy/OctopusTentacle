using System;
using Octopus.Shared.Communications.Logging;

namespace Octopus.Core.Orchestration.Messages.Backup
{
    public class StartBackupCommand : IStartOrchestrationCommand
    {
        readonly string taskId;
        readonly LoggerReference logger;

        public StartBackupCommand(string taskId, LoggerReference logger)
        {
            this.taskId = taskId;
            this.logger = logger;
        }

        public string TaskId
        {
            get { return taskId; }
        }

        public LoggerReference Logger
        {
            get { return logger; }
        }
    }
}