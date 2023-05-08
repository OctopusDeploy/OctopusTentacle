using System;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptState
    {
        [JsonConstructor]
        public ScriptState(ScriptTicket scriptTicket,
            DateTimeOffset created,
            DateTimeOffset? started,
            DateTimeOffset? completed,
            ProcessState state,
            int? exitCode,
            bool? ranToCompletion)
        {
            Created = created;
            ScriptTicket = scriptTicket;
            Started = started;
            Completed = completed;
            State = state;
            ExitCode = exitCode;
            RanToCompletion = ranToCompletion;
        }

        public ScriptState(ScriptTicket scriptTicket, DateTimeOffset created)
        {
            ScriptTicket = scriptTicket;
            Created = created;
        }

        public ScriptTicket ScriptTicket { get; }
        public DateTimeOffset Created { get; }
        public DateTimeOffset? Started { get; private set; }
        public DateTimeOffset? Completed { get; private set; }
        public ProcessState State { get; private set; } = ProcessState.Pending;
        public int? ExitCode { get; private set; }
        public bool? RanToCompletion { get; private set; }

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