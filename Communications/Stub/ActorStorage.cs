using System;
using System.Collections.Generic;
using Pipefish.Persistence;

namespace Octopus.Shared.Communications.Stub
{
    class ActorStorage : IActorStorage
    {
        public IActorState GetStorageFor(string actorName, string initiatingMessageType = null)
        {
            return new ActorState(actorName, initiatingMessageType);
        }

        public IList<IActorState> GetAll()
        {
            return new List<IActorState>();
        }
    }
}
