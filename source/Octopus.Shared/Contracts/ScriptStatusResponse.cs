using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public enum ProcessState
    {
        Pending,
        Running,
        Complete
    }

    public enum ProcessOutputSource
    {
        Debug,
        StdOut,
        StdErr
    }

    public class ProcessOutput
    {
        readonly ProcessOutputSource source;
        readonly string text;
        readonly DateTimeOffset occurred;

        public ProcessOutput(ProcessOutputSource source, string text) : this(source, text, DateTimeOffset.UtcNow)
        {
        }

        [JsonConstructor]
        public ProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred)
        {
            this.source = source;
            this.text = text;
            this.occurred = occurred;
        }

        public ProcessOutputSource Source
        {
            get { return source; }
        }

        public DateTimeOffset Occurred
        {
            get { return occurred; }
        }

        public string Text
        {
            get { return text; }
        }
    }

    public class ScriptStatusResponse
    {
        readonly ScriptTicket ticket;
        readonly ProcessState state;
        readonly int exitCode;
        readonly List<ProcessOutput> logs;
        readonly long nextLogSequence;

        public ScriptStatusResponse(ScriptTicket ticket, ProcessState state, int exitCode, List<ProcessOutput> logs, long nextLogSequence)
        {
            this.ticket = ticket;
            this.state = state;
            this.exitCode = exitCode;
            this.logs = logs;
            this.nextLogSequence = nextLogSequence;
        }

        public ScriptTicket Ticket
        {
            get { return ticket; }
        }

        public List<ProcessOutput> Logs
        {
            get { return logs; }
        }

        public long NextLogSequence
        {
            get { return nextLogSequence; }
        }

        public ProcessState State
        {
            get { return state; }
        }

        public int ExitCode
        {
            get { return exitCode; }
        }
    }
}