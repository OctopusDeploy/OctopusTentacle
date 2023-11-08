using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Caching;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Services.Capabilities
{
    public interface IAsyncCapabilitiesServiceV2
    {
        [CacheResponse(600)]
        Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken);
    }
}