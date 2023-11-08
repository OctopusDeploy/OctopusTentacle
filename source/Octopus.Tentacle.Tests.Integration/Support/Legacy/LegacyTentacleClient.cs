using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class LegacyTentacleClient
    {
        public LegacyTentacleClient(
            IAsyncClientScriptService scriptService,
            IAsyncClientFileTransferService fileTransferService,
            IAsyncClientCapabilitiesServiceV2 capabilitiesServiceV2)
        {
            ScriptService = scriptService;
            FileTransferService = fileTransferService;
            CapabilitiesServiceV2 = capabilitiesServiceV2;
        }

        public IAsyncClientScriptService ScriptService { get; }
        public IAsyncClientFileTransferService FileTransferService { get; }
        public IAsyncClientCapabilitiesServiceV2 CapabilitiesServiceV2 { get; }
    }
}