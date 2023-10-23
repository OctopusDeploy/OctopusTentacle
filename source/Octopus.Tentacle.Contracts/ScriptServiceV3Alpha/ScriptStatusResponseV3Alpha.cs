using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class ScriptStatusResponseV3Alpha
    {
        public ScriptStatusResponseV3Alpha(ScriptTicket scriptTicket,
            ProcessState state,
            int exitCode,
            List<ProcessOutput> logs,
            long nextLogSequence)
        {
            ScriptTicket = scriptTicket;
            State = state;
            ExitCode = exitCode;
            Logs = logs;
            NextLogSequence = nextLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }

        public List<ProcessOutput> Logs { get; }

        public long NextLogSequence { get; }

        public ProcessState State { get; }

        public int ExitCode { get; }
    }
}