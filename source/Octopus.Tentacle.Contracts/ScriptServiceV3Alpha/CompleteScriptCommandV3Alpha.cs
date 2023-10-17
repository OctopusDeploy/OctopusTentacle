using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CompleteScriptCommandV3Alpha
    {
        public CompleteScriptCommandV3Alpha(ScriptTicket ticket)
        {
            Ticket = ticket;
        }

        public ScriptTicket Ticket { get; }
    }
}