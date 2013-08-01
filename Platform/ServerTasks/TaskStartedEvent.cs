using System;
using Pipefish;
using Pipefish.Core;

namespace Octopus.Core.Orchestration.Messages
{
    public class TaskStartedEvent : IMessage
    {
        readonly string taskId;
        readonly ActorId taskHandlerId;

        public TaskStartedEvent(string taskId, ActorId taskHandlerId)
        {
            this.taskId = taskId;
            this.taskHandlerId = taskHandlerId;
        }

        public ActorId TaskHandlerId
        {
            get { return taskHandlerId; }
        }

        public string TaskId
        {
            get { return taskId; }
        }
    }
}