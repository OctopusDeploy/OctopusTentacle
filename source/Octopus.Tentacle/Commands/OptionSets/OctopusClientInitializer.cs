using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class OctopusClientInitializer : IOctopusClientInitializer
    {
        public async Task<IOctopusAsyncClient> CreateAsyncClient(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy)
        {
            IOctopusAsyncClient client = null;
            try
            {
                var endpoint = GetEndpoint(apiEndpointOptions, overrideProxy);
                client = await OctopusAsyncClient.Create(endpoint).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
                {
                    await client.Repository.Users
                        .SignIn(new LoginCommand { Username = apiEndpointOptions.Username, Password = apiEndpointOptions.Password });
                }
                return client;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        public IOctopusClient CreateSyncClient(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy)
        {
            IOctopusClient client = null;
            try
            {
                var endpoint = GetEndpoint(apiEndpointOptions, overrideProxy);
                client = new OctopusClient(endpoint);
                if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
                {
                    var repository = new OctopusRepository(client);
                    repository.Users
                        .SignIn(new LoginCommand { Username = apiEndpointOptions.Username, Password = apiEndpointOptions.Password });
                }
                return client;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }

        OctopusServerEndpoint GetEndpoint(ApiEndpointOptions apiEndpointOptions, IWebProxy overrideProxy)
        {
            OctopusServerEndpoint endpoint;
            if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
            {
                endpoint = new OctopusServerEndpoint(apiEndpointOptions.Server);
                if (overrideProxy != null)
                {
                    endpoint.Proxy = overrideProxy;
                }
            }
            else
            {
                endpoint = new OctopusServerEndpoint(apiEndpointOptions.Server, apiEndpointOptions.ApiKey, credentials: null);
                if (overrideProxy != null)
                {
                    endpoint.Proxy = overrideProxy;
                }
            }
            return endpoint;
        }
    }
}