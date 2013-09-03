using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Pipefish.Core;
using Pipefish.Hosting;
using Pipefish.Transport.HttpClient;

namespace Octopus.Shared.Communications.Conversations
{
    public class ConversationTracker : IMessageInspector
    {
        readonly IEnumerable<TimeSpan> conversationPollingInterval;
        readonly ConcurrentDictionary<string, ConversationState> conversations = new ConcurrentDictionary<string, ConversationState>();

        public ConversationTracker(IEnumerable<TimeSpan> conversationPollingInterval)
        {
            if (conversationPollingInterval == null) throw new ArgumentNullException("conversationPollingInterval");
            this.conversationPollingInterval = conversationPollingInterval;
        }

        public void Track(string squid, IPollingConnection connection)
        {
            if (squid == null) throw new ArgumentNullException("squid");
            if (connection == null) throw new ArgumentNullException("connection");

            if (!conversations.TryAdd(squid, new ConversationState(squid, connection, conversationPollingInterval)))
                throw new InvalidOperationException("Machine is already being tracked");
        }

        public bool StopTracking(string squid)
        {
            ConversationState state;
            return conversations.TryRemove(squid, out state);
        }

        public void OnReceiving(IActor actor, Message message)
        {
            UpdateConversations(message.From.Space, message);
        }

        public void OnReceived(IActor actor, Message message, Exception exceptionIfThrown)
        {
        }

        public void OnSending(IActor actor, Message message)
        {
        }

        public void OnSent(IActor actor, Message message)
        {
            UpdateConversations(message.To.Space, message);
        }

        private void UpdateConversations(string remoteSpace, Message message)
        {
            ConversationState state;
            if (conversations.TryGetValue(remoteSpace, out state))
                state.UpdateConversation(message);
        }
    }
}
