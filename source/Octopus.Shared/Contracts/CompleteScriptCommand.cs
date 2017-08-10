using System;

namespace Octopus.Shared.Contracts
{
    public class CompleteScriptCommand
    {
        readonly ScriptTicket ticket;
        readonly long lastLogSequence;

        public CompleteScriptCommand(ScriptTicket ticket, long lastLogSequence)
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