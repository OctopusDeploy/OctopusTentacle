using System;
using Halibut;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Decorators
{
    /// <summary>
    /// Halibut Listening Client during connection throws the OperationCancelledException wrapped in a HalibutClientException
    /// </summary>
    internal class HalibutExceptionTentacleServiceDecorator : ITentacleServiceDecorator
    {
        public IScriptService Decorate(IScriptService service)
        {
            return service;
        }

        public IScriptServiceV2 Decorate(IScriptServiceV2 service)
        {
            return new HalibutExceptionScriptServiceV2Decorator(service);
        }

        public IFileTransferService Decorate(IFileTransferService service)
        {
            return service;
        }

        public ICapabilitiesServiceV2 Decorate(ICapabilitiesServiceV2 service)
        {
            return new HalibutExceptionCapabilitiesServiceV2Decorator(service);
        }

        public static bool IsHalibutOperationCancellationException(Exception e)
        {
            return e is HalibutClientException && e.Message.Contains("The operation was canceled");
        }
    }

    class HalibutExceptionScriptServiceV2Decorator : IScriptServiceV2
    {
        private readonly IScriptServiceV2 inner;

        public HalibutExceptionScriptServiceV2Decorator(IScriptServiceV2 inner)
        {
            this.inner = inner;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            try
            {
                return inner.StartScript(command);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            try
            {
                return inner.GetStatus(request);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            try
            {
                return inner.CancelScript(command);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            try
            {
                inner.CompleteScript(command);
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }

    class HalibutExceptionCapabilitiesServiceV2Decorator : ICapabilitiesServiceV2
    {
        private readonly ICapabilitiesServiceV2 inner;

        public HalibutExceptionCapabilitiesServiceV2Decorator(ICapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponseV2 GetCapabilities()
        {
            try
            {
                return inner.GetCapabilities();
            }
            catch (Exception e) when (HalibutExceptionTentacleServiceDecorator.IsHalibutOperationCancellationException(e))
            {
                throw new OperationCanceledException("The operation was cancelled", e);
            }
        }
    }
}