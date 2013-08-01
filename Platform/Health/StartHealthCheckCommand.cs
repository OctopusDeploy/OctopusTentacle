using System;
using Octopus.Shared.Communications.Logging;

namespace Octopus.Core.Orchestration.Messages.Health
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