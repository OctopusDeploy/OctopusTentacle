using System;
using Pipefish.Core;

namespace Octopus.Shared.Orchestration.Origination
{
    public class OriginatingMessage
    {
        public Guid Id { get; private set; }
        public ActorId From { get; private set; }

        public OriginatingMessage(Guid id, ActorId @from)
        {
            Id = id;
            From = @from;
        }
    }
}