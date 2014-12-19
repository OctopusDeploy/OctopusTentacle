using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Pipefish.Core;
using Pipefish.Transport;

namespace Octopus.Shared.Communications
{
    public class InMemoryMessageStore : IMessageStore
    {
        readonly ConcurrentDictionary<string, IMessageStoreSubscription> queues = new ConcurrentDictionary<string, IMessageStoreSubscription>();
        readonly object sync = new object();
        readonly Dictionary<string, Queue<Message>> messagesSentBeforeSubscribers = new Dictionary<string, Queue<Message>>();
        
        public InMemoryMessageStore()
        {
        }

        public void Subscribe(IMessageStoreSubscription subscription)
        {
            Queue<Message> sentEarly = null;
            lock (sync)
            {
                if (!queues.TryAdd(subscription.Name, subscription))
                    return;

                if (messagesSentBeforeSubscribers.TryGetValue(subscription.Name, out sentEarly))
                {
                    messagesSentBeforeSubscribers.Remove(subscription.Name);
                }
            }

            while (sentEarly != null && sentEarly.Count > 0)
            {
                subscription.Accept(sentEarly.Dequeue());
            }
        }

        public bool Unsubscribe(string space)
        {
            IMessageStoreSubscription queue;
            return queues.TryRemove(space, out queue);
        }

        public bool TryRevoke(Guid messageId, out Message revokedMessage)
        {
            foreach (var messageStoreSubscription in queues.Values)
            {
                if (messageStoreSubscription.TryRevoke(messageId, out revokedMessage))
                    return true;
            }

            revokedMessage = null;
            return false;
        }

        public void ConfirmAccepted(Message message)
        {
            // Do nothing, we don't support persistent messages
        }

        public void Store(Message message)
        {
            IMessageStoreSubscription subscribedQueue;
            lock (sync)
            {
                if (!queues.TryGetValue(message.To.Space, out subscribedQueue))
                {
                    if (!messagesSentBeforeSubscribers.ContainsKey(message.To.Space))
                    {
                        messagesSentBeforeSubscribers[message.To.Space]= new Queue<Message>();
                    }

                    messagesSentBeforeSubscribers[message.To.Space].Enqueue(message);
                    return;
                }
            }

            subscribedQueue.Accept(message);
        }
    }
}