using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class AsyncToSyncProxy
    {
        public static IClientScriptService ProxyAsyncToSync(IAsyncClientScriptService service)
        {
            return new ClientScriptServiceAsyncToSyncProxy(service);
        }

        public static IClientFileTransferService ProxyAsyncToSync(IAsyncClientFileTransferService service)
        {
            return new ClientFileTransferServiceAsyncToSyncProxy(service);
        }

        public static IClientScriptServiceV2 ProxyAsyncToSync(IAsyncClientScriptServiceV2 service)
        {
            return new ClientScriptServiceV2AsyncToSyncProxy(service);
        }

        public static IClientCapabilitiesServiceV2 ProxyAsyncToSync(IAsyncClientCapabilitiesServiceV2 service)
        {
            return new ClientCapabilitiesServiceV2AsyncToSyncProxy(service);
        }

        public static IAsyncClientScriptService ProxySyncToAsync(IClientScriptService service)
        {
            return new ClientScriptServiceSyncToAsyncProxy(service);
        }

        public static IAsyncClientScriptServiceV2 ProxySyncToAsync(IClientScriptServiceV2 service)
        {
            return new ClientScriptServiceV2SyncToAsyncProxy(service);
        }

        public static IAsyncClientFileTransferService ProxySyncToAsync(IClientFileTransferService service)
        {
            return new ClientFileTransferServiceSyncToAsyncProxy(service);
        }

        public static IAsyncClientCapabilitiesServiceV2 ProxySyncToAsync(IClientCapabilitiesServiceV2 service)
        {
            return new ClientCapabilitiesServiceV2SyncToAsyncProxy(service);
        }
    }
}