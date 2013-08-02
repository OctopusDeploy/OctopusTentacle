using System;
using Pipefish.Core;
using Pipefish.Messages;

namespace Octopus.Shared.Orchestration.Completion
{
    public class CompletesOnTimeout : PersistentAspect<string>
    {
        const string ReminderStateKey = "CompletesOnTimeout.Reminder";

        readonly Action onCompleting;
        readonly TimeSpan timeoutInterval = TimeSpan.FromDays(90);

        public CompletesOnTimeout(Action onCompleting)
            : base(ReminderStateKey)
        {
            this.onCompleting = onCompleting;
        }

        public CompletesOnTimeout()
            : this(() => { })
        {
        }

        public override void OnReceiving(Message message)
        {
            base.OnReceiving(message);

            if (AspectData == null)
            {
                AspectData = Guid.NewGuid().ToString();
                var timeout = DateTime.UtcNow + timeoutInterval;
                var timeoutRequest = new Message(
                    Actor.Id,
                    new ActorId(WellKnownActors.Clock, Space.Name),
                    new SetTimeoutCommand(timeout, AspectData));
                Space.Send(timeoutRequest);
            }
            else
            {
                var elapsed = message.Body as TimeoutElapsedEvent;
                if (elapsed != null && AspectData.Equals(elapsed.Reminder))
                {
                    try
                    {
                        onCompleting();
                    }
                    finally
                    {
                        Space.Detach(Actor);
                    }
                }
            }
        }
    }
}
