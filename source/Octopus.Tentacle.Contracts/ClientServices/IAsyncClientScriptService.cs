using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientScriptService
    {
        Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions);
        Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions halibutProxyRequestOptions);
        Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions);
        Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}