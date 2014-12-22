using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.ServerTasks
{
    public class StartTaskCommand : ICorrelatedMessage
    {
        public string TaskId { get; private set; }
        public ICorrelatedMessage StartCorrelatedMessageBody { get; private set; }
        public LoggerReference Logger { get; private set; }

        public StartTaskCommand(LoggerReference logger, string taskId, ICorrelatedMessage startCorrelatedMessageBody)
        {
            TaskId = taskId;
            Logger = logger;
            StartCorrelatedMessageBody = startCorrelatedMessageBody;
        }
    }
}