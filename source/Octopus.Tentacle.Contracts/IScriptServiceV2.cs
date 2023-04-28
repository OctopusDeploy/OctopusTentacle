using System;

namespace Octopus.Tentacle.Contracts
{
    public interface IScriptServiceV2
    {
        ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command);
        ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request);
        ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command);
        void CompleteScript(CompleteScriptCommandV2 command);
    }
}