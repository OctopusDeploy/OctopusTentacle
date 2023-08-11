using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class SyncAndAsyncCapabilitiesServiceV2 : SyncAndAsyncService<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>
    {
        public SyncAndAsyncCapabilitiesServiceV2(ICapabilitiesServiceV2? syncService, IAsyncClientCapabilitiesServiceV2? asyncService) : base(syncService, asyncService)
        {
        }
    }
}