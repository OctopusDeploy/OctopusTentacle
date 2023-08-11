using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class SyncAndAsyncFileTransferServiceV1 : SyncAndAsyncService<IFileTransferService, IAsyncClientFileTransferService>
    {
        public SyncAndAsyncFileTransferServiceV1(IFileTransferService? syncService, IAsyncClientFileTransferService? asyncService) : base(syncService, asyncService)
        {
        }
    }
}
