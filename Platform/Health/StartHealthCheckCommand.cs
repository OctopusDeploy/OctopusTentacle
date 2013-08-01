using System;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Platform.ServerTasks;

namespace Octopus.Shared.Platform.Health
{
    public class StartHealthCheckCommand : IStartOrchestrationCommand
    {
        readonly LoggerReference logger;
        readonly string taskId;

        public StartHealthCheckCommand(string taskId, LoggerReference logger)
        {
            this.logger = logger;
            this.taskId = taskId;
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