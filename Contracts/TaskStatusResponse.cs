using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Client.Model;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Contracts
{
    public class TaskStatusResponse
    {
        readonly TaskTicket ticket;
        readonly TaskState state;
        readonly List<LogEvent> logs;
        readonly long nextLogSequence;

        public TaskStatusResponse(TaskTicket ticket, TaskState state, List<LogEvent> logs, long nextLogSequence)
        {
            this.ticket = ticket;
            this.state = state;
            this.logs = logs;
            this.nextLogSequence = nextLogSequence;
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

        public long NextLogSequence
        {
            get { return nextLogSequence; }
        }

        [JsonIgnore]
        public bool IsComplete
        {
            get { return !(state == TaskState.Executing || state == TaskState.Queued || state == TaskState.Cancelling); }
        }
    }
}