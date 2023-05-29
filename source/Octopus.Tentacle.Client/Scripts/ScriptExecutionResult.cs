using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public class ScriptExecutionResult
    {
        public ScriptExecutionResult(ProcessState state, int exitCode)
        {
            State = state;
            ExitCode = exitCode;
        }

        public ProcessState State { get; }

        public int ExitCode { get; }
    }
}