using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client
{
    interface ITentacleServiceDecoratorFactory
    {
        public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService);
        
        public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService);

        public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service);

        public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service);
        public IAsyncClientKubernetesScriptServiceV1Alpha Decorate(IAsyncClientKubernetesScriptServiceV1Alpha service);
        public IAsyncClientKubernetesScriptServiceV1 Decorate(IAsyncClientKubernetesScriptServiceV1 service);
    }
}