using System;

namespace Octopus.Shared.Contracts
{
    public class TaskStatusRequest
    {
        readonly TaskTicket ticket;
        readonly long lastLogSequence;

        public TaskStatusRequest(TaskTicket ticket, long lastLogSequence)
        {
            this.ticket = ticket;
            this.lastLogSequence = lastLogSequence;
        }

        public TaskTicket Ticket
        {
            get { return ticket; }
        }

        public long LastLogSequence
        {
            get { return lastLogSequence; }
        }
    }
}