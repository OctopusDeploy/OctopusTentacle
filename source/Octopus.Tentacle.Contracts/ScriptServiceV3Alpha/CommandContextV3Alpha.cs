namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CommandContextV3Alpha
    {
        public CommandContextV3Alpha(
            ScriptTicket scriptTicket,
            long nextLogSequence)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
        }

        public ScriptTicket ScriptTicket { get; }
        
        public long NextLogSequence { get; }
    }
}