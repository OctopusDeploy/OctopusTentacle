using System;

namespace Octopus.Shared.Communications.Conversations
{
    class ActiveConversation
    {
        public Guid InitiatingMessageId { get; private set; }
        public string InitiatingMessageType { get; private set; }

        public ActiveConversation(Guid initiatingMessageId, string initiatingMessageType)
        {
            InitiatingMessageId = initiatingMessageId;
            InitiatingMessageType = initiatingMessageType;
        }
    }
}