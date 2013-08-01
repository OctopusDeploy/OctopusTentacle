using System;
using Pipefish;

namespace Octopus.Shared.Platform.ServerTasks
{
    public class ExecuteTaskCommand : IMessage
    {
        readonly string taskId;
        readonly IMessageWithLogger detailsMessage;

        public ExecuteTaskCommand(string taskId, IMessageWithLogger detailsMessage)
        {
            this.taskId = taskId;
            this.detailsMessage = detailsMessage;
        }

        public string TaskId
        {
            get { return taskId; }
        }

        public IMessageWithLogger DetailsMessage
        {
            get { return detailsMessage; }
        }
    }
}