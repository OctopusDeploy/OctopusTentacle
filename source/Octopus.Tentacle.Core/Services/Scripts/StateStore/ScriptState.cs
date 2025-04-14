using System;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Core.Services.Scripts.StateStore
{
    public class ScriptState
    {
        [JsonConstructor]
        public ScriptState(
            DateTimeOffset created,
            DateTimeOffset? started,
            DateTimeOffset? completed,
            ProcessState state,
            int? exitCode,
            bool? ranToCompletion)
        {
            Created = created;
            Started = started;
            Completed = completed;
            State = state;
            ExitCode = exitCode;
            RanToCompletion = ranToCompletion;
        }

        public ScriptState(DateTimeOffset created)
        {
            Created = created;
        }
        
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