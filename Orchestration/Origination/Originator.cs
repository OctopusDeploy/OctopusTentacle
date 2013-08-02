using System;
using Pipefish;
using Pipefish.Core;

namespace Octopus.Shared.Orchestration.Origination
{
    public class Originator : PersistentAspect<OriginatingMessage>
    {
        const string OriginStateKey = "ActorOrigin.Origin";
        readonly TimeSpan ReplyDefaultTtl = TimeSpan.FromDays(90);

        public Originator() : base(OriginStateKey) { }


        public override void OnReceiving(Message message)
        {
            base.OnReceiving(message);

            if (AspectData == null)
            {
                AspectData = new OriginatingMessage(message.Id, message.From);
            }
        }

        public ActorId From
        {
            get { return AspectData.From; }
        }

        public void Reply(IMessage body, TimeSpan? ttl = null)
        {
            var replyExpiry = DateTime.UtcNow + (ttl ?? ReplyDefaultTtl);
            var reply = new Message(Actor.Id, From, body);
            reply.SetExpiresAt(replyExpiry);
            reply.Headers.Add(ProtocolExtensions.InReplyToHeader, AspectData.Id.ToString());
            Space.Send(reply);
        }
    }
}
