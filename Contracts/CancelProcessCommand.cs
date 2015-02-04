using System;

namespace Octopus.Shared.Contracts
{
    public class CancelProcessCommand
    {
        readonly ProcessTicket ticket;
        readonly long lastLogSequence;

        public CancelProcessCommand(ProcessTicket ticket, long lastLogSequence)
        {
            this.ticket = ticket;
            this.lastLogSequence = lastLogSequence;
        }

        public ProcessTicket Ticket
        {
            get { return ticket; }
        }

        public long LastLogSequence
        {
            get { return lastLogSequence; }
        }
    }
}