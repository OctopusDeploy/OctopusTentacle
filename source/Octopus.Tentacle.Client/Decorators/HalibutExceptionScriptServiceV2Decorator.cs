using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Decorators
{
    class HalibutExceptionScriptServiceV2Decorator : HalibutExceptionTentacleServiceDecorator, IClientScriptServiceV2
    {
        readonly IClientScriptServiceV2 inner;

        public HalibutExceptionScriptServiceV2Decorator(IClientScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return HandleCancellationException(() => inner.StartScript(command, halibutProxyRequestOptions));
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return HandleCancellationException(() => inner.GetStatus(request, halibutProxyRequestOptions));
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return HandleCancellationException(() => inner.CancelScript(command, halibutProxyRequestOptions));
        }

        public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            HandleCancellationException(() => inner.CompleteScript(command, halibutProxyRequestOptions));
        }
    }

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