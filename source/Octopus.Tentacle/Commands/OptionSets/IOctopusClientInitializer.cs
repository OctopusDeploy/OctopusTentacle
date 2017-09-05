using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public interface IOctopusClientInitializer
    {
        Task<IOctopusAsyncClient> CreateAsyncClient(ApiEndpointOptions api, IWebProxy proxyOverride);
        IOctopusClient CreateSyncClient(ApiEndpointOptions api, IWebProxy proxyOverride);
    }
}