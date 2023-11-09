using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Decorators
{
    class HalibutExceptionAsyncScriptServiceV2Decorator : HalibutExceptionTentacleServiceDecorator, IAsyncClientScriptServiceV2
    {
        readonly IAsyncClientScriptServiceV2 inner;

        public HalibutExceptionAsyncScriptServiceV2Decorator(IAsyncClientScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.StartScriptAsync(command, halibutProxyRequestOptions));
        }

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.GetStatusAsync(request, halibutProxyRequestOptions));
        }

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.CancelScriptAsync(command, halibutProxyRequestOptions));
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            await HandleCancellationException(async () => await inner.CompleteScriptAsync(command, halibutProxyRequestOptions));
        }
    }
}