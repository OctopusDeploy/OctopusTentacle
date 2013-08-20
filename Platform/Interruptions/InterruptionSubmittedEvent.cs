using System;
using Pipefish;

namespace Octopus.Shared.Platform.Interruptions
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
