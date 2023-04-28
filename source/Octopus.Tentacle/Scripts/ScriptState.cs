using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptState
    {
        public ScriptState(ScriptTicket scriptTicket)
        {
            ScriptTicket = scriptTicket;
        }

        public ScriptTicket ScriptTicket { get; set; }
        public DateTimeOffset? Started { get; set; }
        public DateTimeOffset? Completed { get; set; }
        public ProcessState State { get; set; } = ProcessState.Pending;
        public int? ExitCode { get; set; }
        public bool? RanToCompletion { get; set; }

        public bool HasStarted()
        {
            return State is ProcessState.Running or ProcessState.Complete;
        }

        public bool HasCompleted()
        {
            return State == ProcessState.Complete;
        }

        public void Start()
        {
            Started = DateTimeOffset.UtcNow;
            State = ProcessState.Running;
        }

        public void Complete(int exitCode, bool ranToCompletion = true)
        {
            Completed = DateTimeOffset.UtcNow;
            ExitCode = exitCode;
            State = ProcessState.Complete;
            RanToCompletion = ranToCompletion;
        }
    }
}