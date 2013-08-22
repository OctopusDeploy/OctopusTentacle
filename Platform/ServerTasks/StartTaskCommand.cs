using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.ServerTasks
{
    public class StartTaskCommand : IMessageWithLogger
    {
        public string TaskId { get; private set; }
        public IMessageWithLogger StartMessageBody { get; private set; }
        public LoggerReference Logger { get; private set; }

        public StartTaskCommand(LoggerReference logger, string taskId, IMessageWithLogger startMessageBody)
        {
            TaskId = taskId;
            Logger = logger;
            StartMessageBody = startMessageBody;
        }
    }
}