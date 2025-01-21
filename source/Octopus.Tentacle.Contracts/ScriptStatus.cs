using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptStatus
    {
        public ProcessState State { get; }
        public int? ExitCode { get; }
        public List<ProcessOutput> Logs { get; }
        
        public ScriptStatus(ProcessState state, int? exitCode, List<ProcessOutput> logs)
        {
            State = state;
            ExitCode = exitCode;
            Logs = logs;
        }
    }
}