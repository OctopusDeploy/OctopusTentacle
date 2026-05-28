using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public class AbandonScriptCommandV2
    {
        public AbandonScriptCommandV2(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}
