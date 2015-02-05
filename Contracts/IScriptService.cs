using System;

namespace Octopus.Shared.Contracts
{
    public interface IScriptService
    {
        ScriptTicket RunScript(RunScriptCommand command);
        ScriptStatusResponse GetStatus(ScriptStatusRequest request);
        ScriptStatusResponse CancelScript(CancelScriptCommand command);
        ScriptStatusResponse CompleteScript(CompleteScriptCommand command);
    }
}