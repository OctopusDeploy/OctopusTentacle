using System;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Services
{
    public class SyncAndAsyncClientCapabilitiesServiceV2 : SyncAndAsyncService<IClientCapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>
    {
        public SyncAndAsyncClientCapabilitiesServiceV2(IClientCapabilitiesServiceV2? syncService, IAsyncClientCapabilitiesServiceV2? asyncService) : base(syncService, asyncService)
        {
        }
    }
}