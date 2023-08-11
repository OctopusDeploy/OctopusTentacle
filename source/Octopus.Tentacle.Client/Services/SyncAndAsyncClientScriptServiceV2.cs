using System;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Services
{
    public class SyncAndAsyncClientScriptServiceV2 : SyncAndAsyncService<IClientScriptServiceV2, IAsyncClientScriptServiceV2>
    {
        public SyncAndAsyncClientScriptServiceV2(IClientScriptServiceV2? syncService, IAsyncClientScriptServiceV2? asyncService) : base(syncService, asyncService)
        {
        }
    }
}