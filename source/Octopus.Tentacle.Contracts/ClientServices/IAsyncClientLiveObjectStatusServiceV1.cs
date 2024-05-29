using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientLiveObjectStatusServiceV1
    {
        Task UpdateResources(string[] resources, HalibutProxyRequestOptions proxyRequestOptions);
    }
    
}