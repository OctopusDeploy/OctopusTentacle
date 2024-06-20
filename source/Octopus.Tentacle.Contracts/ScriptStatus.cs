using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptStatus
    {
        ProcessState state;
        int? exitCode;
        List<ProcessOutput> logs;
        
        public ScriptStatus(ProcessState state, int? exitCode, List<ProcessOutput> logs)
        {
            this.state = state;
            this.exitCode = exitCode;
            this.logs = logs;
        }
    }
}