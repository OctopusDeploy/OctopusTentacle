using System;
using Pipefish.Persistence;

namespace Octopus.Shared.Communications.Stub
{
    class ActorState : IActorState
    {
        public ActorState(string actorName, string initiatingMessageType)
        {
            ActorName = actorName;
            InitiatingMessageType = initiatingMessageType;
        }

        public TData Load<TData>()
        {
            return default(TData);
        }

        public void Save(object state)
        {
        }

        public void Remove()
        {
        }

        public string ActorName { get; private set; }
        public string InitiatingMessageType { get; private set; }
    }
}