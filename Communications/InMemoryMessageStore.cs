using System;
using System.Collections.Concurrent;
using Pipefish.Core;
using Pipefish.Transport;

namespace Octopus.Shared.Communications
{
    public class InMemoryMessageStore : IMessageStore
    {
        readonly ConcurrentDictionary<string, IMessageStoreSubscription> queues = new ConcurrentDictionary<string, IMessageStoreSubscription>();

        public void Subscribe(IMessageStoreSubscription subscription)
        {
            if (!queues.TryAdd(subscription.Name, subscription))
                throw new InvalidOperationException("Space already subscribed");
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
            if (!queues.TryGetValue(message.To.Space, out subscribedQueue))
            {
                return;
            }

            subscribedQueue.Accept(message);
        }
    }
}