using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Caching;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientCapabilitiesServiceV2
    {
        [CacheResponse(600)]
        Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}