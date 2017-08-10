using System;

namespace Octopus.Shared.Contracts
{
    public class CancelScriptCommand
    {
        readonly ScriptTicket ticket;
        readonly long lastLogSequence;

        public CancelScriptCommand(ScriptTicket ticket, long lastLogSequence)
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