using System;
using Pipefish;

namespace Octopus.Shared.Platform.Interruptions
{
    public class InterruptionCompletedEvent : IMessage
    {
        public string InterruptionId { get; private set; }

        public InterruptionCompletedEvent(string interruptionId)
        {
            InterruptionId = interruptionId;
        }
    }
}
