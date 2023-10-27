using System;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Decorators
{
    /// <summary>
    /// Halibut Listening Client during connection throws the OperationCancelledException wrapped in a HalibutClientException
    /// </summary>
    class HalibutExceptionTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
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
    }
}