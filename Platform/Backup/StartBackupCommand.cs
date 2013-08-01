using System;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Platform.ServerTasks;

namespace Octopus.Shared.Platform.Backup
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