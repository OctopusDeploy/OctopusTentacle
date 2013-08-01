using System;
using Pipefish;

namespace Octopus.Core.Orchestration.Messages
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