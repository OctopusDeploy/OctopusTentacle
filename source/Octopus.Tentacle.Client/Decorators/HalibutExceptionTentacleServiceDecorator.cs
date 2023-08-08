using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Decorators
{
    /// <summary>
    /// Halibut Listening Client during connection throws the OperationCancelledException wrapped in a HalibutClientException
    /// </summary>
    internal class HalibutExceptionTentacleServiceDecorator : ITentacleServiceDecorator
    {
        public IClientScriptService Decorate(IClientScriptService service)
        {
            return service;
        }

        public IAsyncClientScriptService Decorate(IAsyncClientScriptService service)
        {
            return service;
        }

        public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 service)
        {
            return new HalibutExceptionScriptServiceV2Decorator(service);
        }

        public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 service)
        {
            return new HalibutExceptionAsyncScriptServiceV2Decorator(service);
        }

        public IClientFileTransferService Decorate(IClientFileTransferService service)
        {
            return service;
        }

        public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service)
        {
            return service;
        }

        public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service)
        {
            return new HalibutExceptionCapabilitiesServiceV2Decorator(service);
        }

        public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service)
        {
            return new HalibutExceptionAsyncCapabilitiesServiceV2Decorator(service);
        }

        public static bool IsHalibutOperationCancellationException(Exception e)
        {
            return e is HalibutClientException && e.Message.Contains("The operation was canceled");
        }
    }

    class HalibutExceptionScriptServiceV2Decorator : IClientScriptServiceV2
    {
        private readonly IClientScriptServiceV2 inner;

        public HalibutExceptionScriptServiceV2Decorator(IClientScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return inner.StartScript(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return inner.GetStatus(request, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return inner.CancelScript(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                inner.CompleteScript(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }

    class HalibutExceptionAsyncScriptServiceV2Decorator : IAsyncClientScriptServiceV2
    {
        private readonly IAsyncClientScriptServiceV2 inner;

        public HalibutExceptionAsyncScriptServiceV2Decorator(IAsyncClientScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return await inner.StartScriptAsync(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return await inner.GetStatusAsync(request, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return await inner.CancelScriptAsync(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                await inner.CompleteScriptAsync(command, halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }

    class HalibutExceptionCapabilitiesServiceV2Decorator : IClientCapabilitiesServiceV2
    {
        private readonly IClientCapabilitiesServiceV2 inner;

        public HalibutExceptionCapabilitiesServiceV2Decorator(IClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return inner.GetCapabilities(halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }

    class HalibutExceptionAsyncCapabilitiesServiceV2Decorator : IAsyncClientCapabilitiesServiceV2
    {
        private readonly IAsyncClientCapabilitiesServiceV2 inner;

        public HalibutExceptionAsyncCapabilitiesServiceV2Decorator(IAsyncClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            try
            {
                return await inner.GetCapabilitiesAsync(halibutProxyRequestOptions);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }
}