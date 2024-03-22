using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Caching;
using Octopus.Tentacle.Contracts.Capabilities;

// Don't "fix" this namespace, this is what we originally did and perhaps
// what we need to keep doing. 
namespace Octopus.Tentacle.Services.Capabilities
{
    public interface IAsyncCapabilitiesServiceV2
    {
        
        [CacheResponse(600)]
        Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken);
    }
}