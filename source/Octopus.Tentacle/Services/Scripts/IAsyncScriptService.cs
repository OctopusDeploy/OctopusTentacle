using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Services.Scripts
{
    public interface IAsyncScriptService
    {
        Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, CancellationToken cancellationToken);
    }
}