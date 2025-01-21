using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.EventDriven
{
    public class CommandContext
    {
        public CommandContext(ScriptTicket scriptTicket,
            long nextLogSequence,
            ScriptServiceVersion scripServiceVersionUsed)
        {
            ScriptTicket = scriptTicket;
            NextLogSequence = nextLogSequence;
            ScripServiceVersionUsed = scripServiceVersionUsed;
        }

        public ScriptTicket ScriptTicket { get; }
        public long NextLogSequence { get; }
        public ScriptServiceVersion ScripServiceVersionUsed { get; }
    }
}