using System;

namespace Octopus.Shared.Contracts
{
    public interface IScriptService
    {
        ScriptTicket StartScript(StartScriptCommand command, bool requiresLock = false);
        ScriptStatusResponse GetStatus(ScriptStatusRequest request);
        ScriptStatusResponse CancelScript(CancelScriptCommand command);
        ScriptStatusResponse CompleteScript(CompleteScriptCommand command);
    }
}