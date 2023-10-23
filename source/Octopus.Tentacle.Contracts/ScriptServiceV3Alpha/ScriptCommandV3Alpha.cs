namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public abstract class ScriptCommandV3Alpha
    {
        public ScriptTicket ScriptTicket { get; }

        protected ScriptCommandV3Alpha(ScriptTicket scriptTicket)
        {
            ScriptTicket = scriptTicket;
        }
    }
}