using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptStatus
    {
        public ProcessState State;
        public int? ExitCode;
        public List<ProcessOutput> Logs;
        
        public ScriptStatus(ProcessState state, int? exitCode, List<ProcessOutput> logs)
        {
            this.State = state;
            this.ExitCode = exitCode;
            this.Logs = logs;
        }
    }
}