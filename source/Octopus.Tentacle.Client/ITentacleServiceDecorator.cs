using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client
{
    internal interface ITentacleServiceDecorator
    {
        public IClientScriptService Decorate(IClientScriptService scriptService);

        public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService);

        public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 scriptService);

        public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService);

        public IClientFileTransferService Decorate(IClientFileTransferService service);

        public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service);

        public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service);

        public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service);
    }
}