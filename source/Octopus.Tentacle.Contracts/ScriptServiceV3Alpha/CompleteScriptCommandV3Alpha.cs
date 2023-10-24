using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CompleteScriptCommandV3Alpha
    {
        public ScriptTicket ScriptTicket { get; }

        public CompleteScriptCommandV3Alpha(ScriptTicket scriptTicket)
        {
            ScriptTicket = scriptTicket;
        }
    }
}