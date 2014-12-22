using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.ServerTasks
{
    public class TaskCompletedEvent : IMessage
    {
        readonly string taskId;
        readonly bool wasSuccessful;
        readonly string errorMessage;

        public TaskCompletedEvent(string taskId, bool wasSuccessful, string errorMessage)
        {
            if (taskId == null) throw new ArgumentNullException("taskId");
            this.taskId = taskId;
            this.wasSuccessful = wasSuccessful;
            this.errorMessage = errorMessage;
        }

        public string TaskId
        {
            get { return taskId; }
        }

        public bool WasSuccessful
        {
            get { return wasSuccessful; }
        }

        public string ErrorMessage
        {
            get { return errorMessage; }
        }

        public static TaskCompletedEvent Success(string taskId)
        {
            return new TaskCompletedEvent(taskId, true, null);
        }

        public static TaskCompletedEvent Failed(string taskId, string errorMessage)
        {
            return new TaskCompletedEvent(taskId, false, errorMessage);
        }
    }
}