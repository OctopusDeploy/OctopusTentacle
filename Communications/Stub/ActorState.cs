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

        public ActorStateDictionary Load()
        {
            return null;
        }

        public void Save(ActorStateDictionary state)
        {

        }

        public void Remove()
        {
        }

        public string ActorName { get; private set; }
        public string InitiatingMessageType { get; private set; }
    }
}