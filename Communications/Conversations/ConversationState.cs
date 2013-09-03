using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Diagnostics;
using Pipefish.Core;
using Pipefish.Transport.HttpClient;

namespace Octopus.Shared.Communications.Conversations
{
    class ConversationState
    {
        readonly string remoteSpace;
        readonly IPollingConnection connection;
        readonly IList<TimeSpan> conversationPollingSequence;

        readonly object sync = new object();
        readonly IDictionary<Guid, ActiveConversation> conversations = new Dictionary<Guid, ActiveConversation>();
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

        public void UpdateConversation(Message message)
        {
            if (message.To.Space == remoteSpace)
            {
                TryBegin(message);
            }
            else if (message.From.Space == remoteSpace)
            {
                TryEnd(message);
            }
            else
            {
                throw new InvalidOperationException("Message doesn't relate to this tracked space");
            }
        }

        void TryBegin(Message message)
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
                        Log.Octopus().Verbose("A new conversation is beginning with " + remoteSpace);
                        preConversationPollingSequence = connection.GetPollingSequence();
                    }

                    connection.SetPollingSequence(conversationPollingSequence);

                    if (conversations.Count > 100)
                    {
                        Log.Octopus().Warn("It looks as though conversations with " + remoteSpace + " are leaking");
                    }
                }
            }
        }

        void TryEnd(Message message)
        {
            lock (sync)
            {
                if (conversations.Any())
                {
                    var inReply = message.TryGetInReplyTo();
                    ActiveConversation ended;
                    if (inReply.HasValue && conversations.TryGetValue(inReply.Value, out ended))
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
        }
    }
}