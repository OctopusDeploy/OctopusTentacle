using System;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Services
{
    public class SyncAndAsyncClientScriptServiceV1 : SyncAndAsyncService<IClientScriptService, IAsyncClientScriptService>
    {
        public SyncAndAsyncClientScriptServiceV1(IClientScriptService? syncService, IAsyncClientScriptService? asyncService) : base(syncService, asyncService)
        {
        }
    }
}
