using System;
using System.Collections.Concurrent;
using Pipefish;
using Pipefish.Transport;

namespace Octopus.Shared.Communications.Stub
{
    class MessageStore : IMessageStore
    {
        readonly ConcurrentDictionary<string, ConcurrentQueue<Message>> queues = new ConcurrentDictionary<string, ConcurrentQueue<Message>>();

        public void Subscribe(string spaceName, ConcurrentQueue<Message> queue)
        {
            if (!queues.TryAdd(spaceName, queue))
                throw new InvalidOperationException("Space already subscribed");
        }

        public void ConfirmAccepted(Message message)
        {
            // Do nothing, we don't support persistent messages
        }

        public void Store(Message message)
        {
            ConcurrentQueue<Message> subscribedQueue;
            if (!queues.TryGetValue(message.To.Space, out subscribedQueue))
                throw new InvalidOperationException("No subscriber exists");

            subscribedQueue.Enqueue(message);
        }
    }
}
