using System;

namespace Octopus.Shared.Contracts
{
    public class ProcessStatusRequest
    {
        readonly ProcessTicket ticket;
        readonly long lastLogSequence;

        public ProcessStatusRequest(ProcessTicket ticket, long lastLogSequence)
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