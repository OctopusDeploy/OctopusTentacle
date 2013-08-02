using System;
using System.Collections.Generic;
using Pipefish;
using Pipefish.Core;
using Pipefish.Persistence;
using Pipefish.Standard;

namespace Octopus.Shared.Orchestration.Origination
{
    public class Originator : Aspect
    {
        const string OriginStateKey = "ActorOrigin.Origin";
        readonly TimeSpan ReplyDefaultTtl = TimeSpan.FromDays(90);
        OriginatingMessage originatingMessage;

        public override void Attach(IActor actor, IActivitySpace space)
        {
            base.Attach(actor, space);

            var persistent = actor as IPersistentActor;
            if (persistent == null) return;

            persistent.AfterLoading += () => LoadOrigin(persistent.State);
            persistent.BeforeSaving += () => SaveOrigin(persistent.State);
        }

        public override void OnReceiving(Message message)
        {
            base.OnReceiving(message);

            if (originatingMessage == null)
            {
                originatingMessage = new OriginatingMessage(message.Id, message.From);
            }
        }

        void LoadOrigin(IDictionary<string, object> state)
        {
            object savedOrigin;
            if (state.TryGetValue(OriginStateKey, out savedOrigin))
            {
                originatingMessage = (OriginatingMessage)savedOrigin;
            }
        }

        void SaveOrigin(IDictionary<string, object> state)
        {
            if (originatingMessage != null  && !state.ContainsKey(OriginStateKey))
                state.Add(OriginStateKey, originatingMessage);
        }

        public ActorId From
        {
            get { return originatingMessage.From; }
        }

        public void Reply(IMessage body, TimeSpan? ttl = null)
        {
            var replyExpiry = DateTime.UtcNow + (ttl ?? ReplyDefaultTtl);
            var reply = new Message(Actor.Id, From, body);
            reply.SetExpiresAt(replyExpiry);
            reply.Headers.Add(ProtocolExtensions.InReplyToHeader, originatingMessage.Id.ToString());
            Space.Send(reply);
        }
    }
}
