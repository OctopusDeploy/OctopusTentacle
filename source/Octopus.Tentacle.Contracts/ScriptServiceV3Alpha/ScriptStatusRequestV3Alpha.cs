using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class ScriptStatusRequestV3Alpha
    {
        public ScriptStatusRequestV3Alpha(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}