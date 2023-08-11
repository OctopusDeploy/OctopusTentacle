using System;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Services
{
    public class SyncAndAsyncClientFileTransferServiceV1 : SyncAndAsyncService<IClientFileTransferService, IAsyncClientFileTransferService>
    {
        public SyncAndAsyncClientFileTransferServiceV1(IClientFileTransferService? syncService, IAsyncClientFileTransferService? asyncService) : base(syncService, asyncService)
        {
        }
    }
}
