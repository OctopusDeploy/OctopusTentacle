using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.EventDriven
{
    /// <summary>
    /// This class holds the context of where we are up to within the script execution life cycle.
    /// When executing a script, there are several stages it goes through (e.g. starting the script, periodically checking status for completion, completing the script).
    /// To be able to progress through these cycles in an event-driven environment, we need to remember some state, and then keep passing that state back into the script executor.
    /// </summary>
    public class CommandContext
    {
        public CommandContext(ScriptTicket scriptTicket,
            long nextLogSequence,
            ScriptServiceVersion scripServiceVersionUsed,
            bool supportsAbandon = false)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
            ScripServiceVersionUsed = scripServiceVersionUsed;
            SupportsAbandon = supportsAbandon;
        }

        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }
        public ScriptServiceVersion ScripServiceVersionUsed { get; }

        // Whether the Tentacle advertised the AbandonScript capability. Determined once from
        // capabilities when the script starts and carried on the start result so the orchestrator
        // can gate abandon escalation on it.
        public bool SupportsAbandon { get; }
    }
}