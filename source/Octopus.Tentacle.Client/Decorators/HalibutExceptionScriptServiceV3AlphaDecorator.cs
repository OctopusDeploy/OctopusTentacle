using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Decorators
{
    class HalibutExceptionScriptServiceV3AlphaDecorator : HalibutExceptionTentacleServiceDecorator, IAsyncClientScriptServiceV3Alpha
    {
        readonly IAsyncClientScriptServiceV3Alpha inner;

        public HalibutExceptionScriptServiceV3AlphaDecorator(IAsyncClientScriptServiceV3Alpha inner)
        {
            this.inner = inner;
        }

        public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.StartScriptAsync(command, halibutProxyRequestOptions));
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.GetStatusAsync(request, halibutProxyRequestOptions));
        }

        public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.CancelScriptAsync(command, halibutProxyRequestOptions));
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            await HandleCancellationException(async () => await inner.CompleteScriptAsync(command, halibutProxyRequestOptions));
        }
    }
}