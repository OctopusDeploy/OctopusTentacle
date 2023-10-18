using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public interface IScriptServiceV3Alpha
    {
        ScriptStatusResponseV3Alpha StartScript(StartScriptCommandV3Alpha command);
        ScriptStatusResponseV3Alpha GetStatus(ScriptStatusRequestV3Alpha request);
        ScriptStatusResponseV3Alpha CancelScript(CancelScriptCommandV3Alpha command);
        void CompleteScript(CompleteScriptCommandV3Alpha command);
    }

    public interface IAsyncScriptServiceV3Alpha
    {
        Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken);
    }
}