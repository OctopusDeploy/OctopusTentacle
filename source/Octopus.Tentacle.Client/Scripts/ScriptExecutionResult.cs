using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public class ScriptExecutionResult
    {
        public ScriptExecutionResult(ScriptTicket ticket,
            ProcessState state,
            int exitCode)
        {
            Ticket = ticket;
            State = state;
            ExitCode = exitCode;
        }

        public ScriptTicket Ticket { get; }

        public ProcessState State { get; }

        public int ExitCode { get; }
    }
}