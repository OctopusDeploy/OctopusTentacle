using System;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Decorators
{
    /// <summary>
    /// Halibut Listening Client during connection throws the OperationCancelledException wrapped in a HalibutClientException
    /// </summary>
    class HalibutExceptionTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
    {
        public IAsyncClientScriptService Decorate(IAsyncClientScriptService service)
        {
            return service;
        }

        public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 service)
        {
            return new HalibutExceptionAsyncScriptServiceV2Decorator(service);
        }

        public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service)
        {
            return service;
        }

        public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service)
        {
            return new HalibutExceptionAsyncCapabilitiesServiceV2Decorator(service);
        }

        public IAsyncClientScriptServiceV3Alpha Decorate(IAsyncClientScriptServiceV3Alpha service)
        {
            return new HalibutExceptionScriptServiceV3AlphaDecorator(service);
        }
    }
}