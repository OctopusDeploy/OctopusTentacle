using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class OctopusClientInitializer : IOctopusClientInitializer
    {
        public async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy)
        {
            return await CreateClient(apiEndpointOptions, overrideProxy, false);
        }

        public async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, bool useDefaultProxy)
        {
            return await CreateClient(apiEndpointOptions, null, useDefaultProxy);
        }

        private async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy, bool useDefaultProxy)
        {
            IOctopusAsyncClient client = null;
            try
            {
                var endpoint = GetEndpoint(apiEndpointOptions, overrideProxy);
                var clientOptions = new OctopusClientOptions { AllowDefaultProxy = useDefaultProxy };
                client = await OctopusAsyncClient.Create(endpoint, clientOptions).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
                    await client.Repository.Users
                        .SignIn(new LoginCommand { Username = apiEndpointOptions.Username, Password = apiEndpointOptions.Password });

                return client;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        private OctopusServerEndpoint GetEndpoint(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy)
        {
            OctopusServerEndpoint endpoint;
            if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
            {
                endpoint = new OctopusServerEndpoint(apiEndpointOptions.Server);
                if (overrideProxy != null) endpoint.Proxy = overrideProxy;
            }
            else
            {
                endpoint = new OctopusServerEndpoint(apiEndpointOptions.Server, apiEndpointOptions.ApiKey, null);
                if (overrideProxy != null) endpoint.Proxy = overrideProxy;
            }

            return endpoint;
        }
    }
}