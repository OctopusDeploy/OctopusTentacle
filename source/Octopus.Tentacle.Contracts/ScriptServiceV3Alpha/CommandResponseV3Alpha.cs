using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CommandResponseV3Alpha
    {
        public CommandResponseV3Alpha(
            CommandContextV3Alpha nextCommandContext,
            ProcessState state,
            int exitCode,
            List<ProcessOutput> logs)
        {
            NextCommandContext = nextCommandContext;
            State = state;
            ExitCode = exitCode;
            Logs = logs;
        }

        public CommandContextV3Alpha NextCommandContext { get; }

        public List<ProcessOutput> Logs { get; }
        
        public ProcessState State { get; }

        public int ExitCode { get; }
    }
}