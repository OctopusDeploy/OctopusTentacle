using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class OctopusClientInitializer : IOctopusClientInitializer
    {
        public async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, IWebProxy? overrideProxy)
            => await CreateClient(apiEndpointOptions, overrideProxy, false);

        public async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, bool useDefaultProxy)
            => await CreateClient(apiEndpointOptions, null, useDefaultProxy);

        async Task<IOctopusAsyncClient> CreateClient(ApiEndpointOptions apiEndpointOptions, IWebProxy? overrideProxy, bool useDefaultProxy)
        {
            IOctopusAsyncClient? client = null;
            try
            {
                var endpoint = GetEndpoint(apiEndpointOptions, overrideProxy);
#if HTTP_CLIENT_SUPPORTS_SSL_OPTIONS
                 var clientOptions = new OctopusClientOptions { AllowDefaultProxy = useDefaultProxy, IgnoreSslErrors = apiEndpointOptions.IgnoreSslErrors };
#else
                var clientOptions = new OctopusClientOptions { AllowDefaultProxy = useDefaultProxy};
#endif

                client = await OctopusAsyncClient.Create(endpoint, clientOptions).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey) &&
                    string.IsNullOrWhiteSpace(apiEndpointOptions.BearerToken))
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

        OctopusServerEndpoint GetEndpoint(ApiEndpointOptions apiEndpointOptions, IWebProxy? overrideProxy)
        {
            if (!string.IsNullOrWhiteSpace(apiEndpointOptions.BearerToken))
            {
                return AddProxy(OctopusServerEndpoint.CreateWithBearerToken(apiEndpointOptions.Server, apiEndpointOptions.BearerToken));
            }

            if (!string.IsNullOrWhiteSpace(apiEndpointOptions.ApiKey))
            {
                return AddProxy(OctopusServerEndpoint.CreateWithApiKey(apiEndpointOptions.Server, apiEndpointOptions.ApiKey));
            }

            return AddProxy(new OctopusServerEndpoint(apiEndpointOptions.Server));

            OctopusServerEndpoint AddProxy(OctopusServerEndpoint endpoint)
            {
                if (overrideProxy != null)
                {
                    endpoint.Proxy = overrideProxy;
                }

                return endpoint;
            }
        }
    }
}