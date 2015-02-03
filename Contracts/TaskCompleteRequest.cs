using System;

namespace Octopus.Shared.Contracts
{
    public class TaskCompleteRequest
    {
        readonly TaskTicket ticket;

        public TaskCompleteRequest(TaskTicket ticket)
        {
            this.ticket = ticket;
        }

        public TaskTicket Ticket
        {
            get { return ticket; }
        }
    }
}