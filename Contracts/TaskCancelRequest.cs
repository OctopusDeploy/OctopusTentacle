using System;

namespace Octopus.Shared.Contracts
{
    public class TaskCancelRequest
    {
        readonly TaskTicket ticket;

        public TaskCancelRequest(TaskTicket ticket)
        {
            this.ticket = ticket;
        }

        public TaskTicket Ticket
        {
            get { return ticket; }
        }
    }
}