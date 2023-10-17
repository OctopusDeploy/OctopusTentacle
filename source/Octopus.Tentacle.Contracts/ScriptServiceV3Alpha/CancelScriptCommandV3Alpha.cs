using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CancelScriptCommandV3Alpha
    {
        public CancelScriptCommandV3Alpha(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}