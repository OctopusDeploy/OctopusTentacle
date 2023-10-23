using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CancelScriptCommandV3Alpha : ScriptCommandV3Alpha
    {
        public CancelScriptCommandV3Alpha(ScriptTicket scriptTicket, long lastLogSequence)
            : base(scriptTicket)
        {
            LastLogSequence = lastLogSequence;
        }

        public long LastLogSequence { get; }
    }
}