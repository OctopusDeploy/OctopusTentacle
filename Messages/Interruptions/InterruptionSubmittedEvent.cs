using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Interruptions
{
    public class InterruptionSubmittedEvent : IMessage
    {
        public string InterruptionId { get; private set; }

        public InterruptionSubmittedEvent(string interruptionId)
        {
            InterruptionId = interruptionId;
        }
    }
}
