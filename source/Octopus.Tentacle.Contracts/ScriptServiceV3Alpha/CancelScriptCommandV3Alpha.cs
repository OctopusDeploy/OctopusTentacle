using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CancelScriptCommandV3Alpha
    {
        public CancelScriptCommandV3Alpha(ScriptTicket scriptTicket, long lastLogSequence)
        {
            ScriptTicket = scriptTicket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        public long LastLogSequence { get; }
    }
}