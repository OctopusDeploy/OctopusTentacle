using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class ScriptStatusRequestV3Alpha
    {
        public ScriptStatusRequestV3Alpha(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}