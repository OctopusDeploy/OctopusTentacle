using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class SyncAndAsyncScriptServiceV1 : SyncAndAsyncService<IScriptService, IAsyncClientScriptService>
    {
        public SyncAndAsyncScriptServiceV1(IScriptService? syncService, IAsyncClientScriptService? asyncService) : base(syncService, asyncService)
        {
        }
    }
}
