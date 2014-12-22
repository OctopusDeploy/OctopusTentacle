using System;
using Pipefish;

namespace Octopus.Shared.Messages.Interruptions
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
