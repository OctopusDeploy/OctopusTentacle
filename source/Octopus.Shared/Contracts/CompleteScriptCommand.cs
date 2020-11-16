using System;

namespace Octopus.Shared.Contracts
{
    public class CompleteScriptCommand
    {
        public CompleteScriptCommand(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}