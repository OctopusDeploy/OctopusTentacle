using System;
using Pipefish;

namespace Octopus.Shared.Platform.ServerTasks
{
    public class StartTaskCommand : IMessage
    {
        public string TaskId { get; private set; }
        public IMessageWithLogger StartMessageBody { get; private set; }

        public StartTaskCommand(string taskId, IMessageWithLogger startMessageBody)
        {
            TaskId = taskId;
            StartMessageBody = startMessageBody;
        }
    }
}