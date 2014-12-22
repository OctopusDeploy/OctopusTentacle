using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.ServerTasks
{
    public class ExecuteTaskCommand : IMessage
    {
        readonly string taskId;
        readonly ICorrelatedMessage detailsCorrelatedMessage;

        public ExecuteTaskCommand(string taskId, ICorrelatedMessage detailsCorrelatedMessage)
        {
            this.taskId = taskId;
            this.detailsCorrelatedMessage = detailsCorrelatedMessage;
        }

        public string TaskId
        {
            get { return taskId; }
        }

        public ICorrelatedMessage DetailsCorrelatedMessage
        {
            get { return detailsCorrelatedMessage; }
        }
    }
}