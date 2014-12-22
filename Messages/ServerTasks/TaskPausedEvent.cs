using System;
using Pipefish;

namespace Octopus.Shared.Messages.ServerTasks
{
    public class TaskPausedEvent : IMessage
    {
        readonly string taskId;

        public TaskPausedEvent(string taskId)
        {
            this.taskId = taskId;
        }

        public string TaskId
        {
            get { return taskId; }
        }
    }
}