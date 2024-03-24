using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class GetStatusCommandV3Alpha
    {
        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }

        public GetStatusCommandV3Alpha(
            ScriptTicket scriptTicket,
            long nextLogSequence)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
        }
    }
}