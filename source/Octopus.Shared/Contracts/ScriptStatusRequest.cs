using System;

namespace Octopus.Shared.Contracts
{
    public class ScriptStatusRequest
    {
        public ScriptStatusRequest(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}