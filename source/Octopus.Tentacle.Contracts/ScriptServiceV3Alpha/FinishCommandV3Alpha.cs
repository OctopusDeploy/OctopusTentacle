using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class FinishCommandV3Alpha
    {
        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }

        public FinishCommandV3Alpha(
            ScriptTicket scriptTicket,
            long nextLogSequence)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
        }
    }
}