using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class ScriptStatusRequestV3Alpha : ScriptCommandV3Alpha
    {
        public ScriptStatusRequestV3Alpha(ScriptTicket scriptTicket, long lastLogSequence)
            : base(scriptTicket)
        {
            LastLogSequence = lastLogSequence;
        }

        public long LastLogSequence { get; }
    }
}