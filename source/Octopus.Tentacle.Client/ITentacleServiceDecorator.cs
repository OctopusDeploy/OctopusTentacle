using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client
{
    internal interface ITentacleServiceDecorator
    {
        public IClientScriptService Decorate(IClientScriptService scriptService);

        public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 scriptService);

        public IClientFileTransferService Decorate(IClientFileTransferService service);

        public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service);
    }
}