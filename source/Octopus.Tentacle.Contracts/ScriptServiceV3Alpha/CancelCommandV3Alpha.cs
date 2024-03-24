using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CancelCommandV3Alpha
    {
        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }

        public CancelCommandV3Alpha(
            ScriptTicket scriptTicket,
            long nextLogSequence)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
        }
    }
}