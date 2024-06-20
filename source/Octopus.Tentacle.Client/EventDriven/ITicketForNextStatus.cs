using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.EventDriven
{
    public interface ITicketForNextStatus
    {

        ScriptTicket ScriptTicket { get; }
        long NextLogSequence { get; }
        public ScriptServiceVersion WhichService { get; }
        
        // TODO does it actually make sense to all of these properties? Perhaps instead we should just expose this concept of
        // serialize the entire thing. That way the driver only needs to know how to save something down for the next time it
        // wants to continue script execution.
    }
    
    public class DefaultTicketForNextStatus : ITicketForNextStatus
    {
        public DefaultTicketForNextStatus(ScriptTicket scriptTicket,
            long nextLogSequence,
            ScriptServiceVersion whichService)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
            WhichService = whichService;
        }

        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }
        public ScriptServiceVersion WhichService { get; }
    }
}