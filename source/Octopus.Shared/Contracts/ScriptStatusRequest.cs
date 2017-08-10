using System;

namespace Octopus.Shared.Contracts
{
    public class ScriptStatusRequest
    {
        readonly ScriptTicket ticket;
        readonly long lastLogSequence;

        public ScriptStatusRequest(ScriptTicket ticket, long lastLogSequence)
        {
            this.ticket = ticket;
            this.lastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket
        {
            get { return ticket; }
        }

        public long LastLogSequence
        {
            get { return lastLogSequence; }
        }
    }
}