using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Messages.Conversations;
using Pipefish.Core;
using Pipefish.Transport.SecureTcp.MessageExchange.Client;

namespace Octopus.Shared.Communications.Conversations
{
    class ConversationState
    {
        readonly string remoteSpace;
        readonly IPollingConnection connection;
        readonly IList<TimeSpan> conversationPollingSequence;

        readonly object sync = new object();
        readonly IDictionary<Guid, ActiveConversation> conversations = new Dictionary<Guid, ActiveConversation>();
        readonly HashSet<string> synonyms = new HashSet<string>();
        IList<TimeSpan> preConversationPollingSequence;

        public ConversationState(string remoteSpace, IPollingConnection connection, IEnumerable<TimeSpan> conversationPollingSequence)
        {
            if (remoteSpace == null) throw new ArgumentNullException("remoteSpace");
            if (connection == null) throw new ArgumentNullException("connection");
            if (conversationPollingSequence == null) throw new ArgumentNullException("conversationPollingSequence");
            this.remoteSpace = remoteSpace;
            this.connection = connection;
            this.conversationPollingSequence = conversationPollingSequence.ToList();
        }

        public void TryBegin(Message message)
        {
            var bodyType = message.Body.GetType();

            var begin = bodyType
                .GetCustomAttributes(typeof(ExpectReplyAttribute), false)
                .Cast<ExpectReplyAttribute>()
                .FirstOrDefault();

            lock (sync)
            {
                if (begin != null)
                {
                    conversations.Add(message.Id, new ActiveConversation(message.Id, message.MessageType));
                    if (conversations.Count == 1)
                    {
                        Log.Octopus().Verbose("A new conversation is beginning with " + Description);
                        preConversationPollingSequence = connection.GetPollingSequence();
                    }

                    connection.SetPollingSequence(conversationPollingSequence);

                    if (conversations.Count > 100)
                    {
                        Log.Octopus().Warn("It looks as though conversations with " + Description + " are leaking");
                    }
                }
                else
                {
                    // When a conversation is active, any incoming message will automatically 'reset' the polling sequence back to the start
                    connection.SetPollingSequence(conversationPollingSequence);
                }
            }
        }

        string Description
        {
            get
            {
                var result = remoteSpace;
                if (synonyms.Count > 0)
                {
                    result += " (hosting " + string.Join(", ", synonyms) + ")";
                }
                return result;
            }
        }

        public void TryEnd(Message message)
        {
            var inReply = message.TryGetInReplyTo();
            if (!inReply.HasValue)
                return;

            TryEnd(inReply.Value);
        }

        public void TryRevoke(Message message)
        {
            TryEnd(message.Id);
        }

        void TryEnd(Guid initiating)
        {
            lock (sync)
            {
                ActiveConversation ended;
                if (conversations.TryGetValue(initiating, out ended))
                {
                    conversations.Remove(ended.InitiatingMessageId);
                    if (conversations.Count == 0)
                    {
                        Log.Octopus().Verbose("The conversation with " + remoteSpace + " has ended");
                        connection.SetPollingSequence(preConversationPollingSequence);
                    }
                }
            }
        }

        public IList<ActiveConversationDescription> GetActiveConversations()
        {
            lock (sync)
            {
                return conversations.Values
                    .Select(c => new ActiveConversationDescription(
                        c.StartedAtUtc, c.InitiatingMessageId, "Expecting reply to: " + c.InitiatingMessageType))
                    .OrderBy(c => c.StartedAtUtc)
                    .ToList();
            }
        }

        public void AddSynonym(string synonym)
        {
            lock (sync)
            {
                synonyms.Add(synonym);
            }
        }

        public void RemoveSynonym(string synonym)
        {
            lock (sync)
            {
                synonyms.Remove(synonym);
            }
        }
    }
}