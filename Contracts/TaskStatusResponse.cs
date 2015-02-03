using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Contracts
{
    public class TaskStatusResponse
    {
        readonly TaskTicket ticket;
        readonly TaskState state;
        readonly List<LogEvent> logs;

        public TaskStatusResponse(TaskTicket ticket, TaskState state, List<LogEvent> logs)
        {
            this.ticket = ticket;
            this.state = state;
            this.logs = logs;
        }

        public TaskState State
        {
            get { return state; }
        }

        public TaskTicket Ticket
        {
            get { return ticket; }
        }

        public List<LogEvent> Logs
        {
            get { return logs; }
        }
    }
}