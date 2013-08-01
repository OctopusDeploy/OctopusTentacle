using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Communications.Conversations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BeginsConversationEndedByAttribute : Attribute
    {
        public BeginsConversationEndedByAttribute(Type endingMessageType, params Type[] otherEndingMessageTypes)
        {
            if (endingMessageType == null) throw new ArgumentNullException("endingMessageType");

            var all = (otherEndingMessageTypes ?? Enumerable.Empty<Type>()).Union(new[] { endingMessageType }).ToArray();
            EndingMessageTypes = all;
        }

        public IEnumerable<Type> EndingMessageTypes { get; private set; }
    }
}
