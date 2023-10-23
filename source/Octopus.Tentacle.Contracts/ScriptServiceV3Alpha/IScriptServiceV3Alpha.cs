using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public interface IScriptServiceV3Alpha
    {
        ScriptStatusResponseV3Alpha StartScript(StartScriptCommandV3Alpha command);
        ScriptStatusResponseV3Alpha GetStatus(ScriptStatusRequestV3Alpha request);
        ScriptStatusResponseV3Alpha CancelScript(CancelScriptCommandV3Alpha command);
        void CompleteScript(CompleteScriptCommandV3Alpha command);
    }
}