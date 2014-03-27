using System;

namespace Octopus.Shared.Communications.Conversations
{
    public class ActiveConversationDescription
    {
        public DateTime StartedAtUtc { get; private set; }
        public Guid InitialMessageId { get; private set; }
        public string InitialMessageDescription { get; private set; }

        public ActiveConversationDescription(DateTime startedAtUtc, Guid initialMessageId, string initialMessageDescription)
        {
            StartedAtUtc = startedAtUtc;
            InitialMessageId = initialMessageId;
            InitialMessageDescription = initialMessageDescription;
        }
    }
}