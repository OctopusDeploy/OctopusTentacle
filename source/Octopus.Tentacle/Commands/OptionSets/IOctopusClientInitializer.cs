using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public interface IOctopusClientInitializer
    {
        Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, IWebProxy? overrideProxy);
        Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, bool useDefaultProxy);
    }
}