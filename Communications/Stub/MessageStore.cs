using System;
using System.Collections.Concurrent;
using Pipefish.Core;
using Pipefish.Transport;

namespace Octopus.Shared.Communications.Stub
{
    class MessageStore : IMessageStore
    {
        readonly ConcurrentDictionary<string, ConcurrentQueue<Message>> queues = new ConcurrentDictionary<string, ConcurrentQueue<Message>>();
        readonly ConcurrentDictionary<string, ConcurrentQueue<Message>> stash = new ConcurrentDictionary<string, ConcurrentQueue<Message>>();

        public void Subscribe(string spaceName, ConcurrentQueue<Message> queue)
        {
            if (!queues.TryAdd(spaceName, queue))
                throw new InvalidOperationException("Space already subscribed");

            ConcurrentQueue<Message> waiting;
            if (stash.TryRemove(spaceName, out waiting))
            {
                Message w;
                while (waiting.TryDequeue(out w))
                    queue.Enqueue(w);
            }
        }

        public bool Unsubscribe(string space)
        {
            ConcurrentQueue<Message> unused;
            return queues.TryRemove(space, out unused);
        }

        public void ConfirmAccepted(Message message)
        {
            // Do nothing, we don't support persistent messages
        }

        public void Store(Message message)
        {
            ConcurrentQueue<Message> subscribedQueue;
            if (queues.TryGetValue(message.To.Space, out subscribedQueue))
            {
                subscribedQueue.Enqueue(message);
            }
            else
            {
                var waiting = stash.GetOrAdd(message.To.Space, s => new ConcurrentQueue<Message>());
                waiting.Enqueue(message);
            }
        }
    }
}
