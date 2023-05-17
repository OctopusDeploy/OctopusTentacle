using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client
{
    public interface ITentacleServiceDecorator
    {
        public IScriptService Decorate(IScriptService scriptService);

        public IScriptServiceV2 Decorate(IScriptServiceV2 scriptService);

        public IFileTransferService Decorate(IFileTransferService service);

        public ICapabilitiesServiceV2 Decorate(ICapabilitiesServiceV2 service);
    }
}