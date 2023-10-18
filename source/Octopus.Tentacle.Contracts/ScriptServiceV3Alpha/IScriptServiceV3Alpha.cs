using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public interface IScriptServiceV3Alpha
    {
        Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command);
        Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request);
        Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command);
        Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command);
    }
}