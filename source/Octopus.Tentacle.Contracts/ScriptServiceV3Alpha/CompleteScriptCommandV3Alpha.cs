using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class CompleteScriptCommandV3Alpha : ScriptCommandV3Alpha
    {
        public CompleteScriptCommandV3Alpha(ScriptTicket scriptTicket)
            : base(scriptTicket)
        {
        }
    }
}