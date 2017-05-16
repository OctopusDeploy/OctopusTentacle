using System;
using System.Net;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class ApiEndpointOptions : ICommandOptions
    {
        string server;
        string apiKey;
        string username;
        string password;

        public ApiEndpointOptions(OptionSet options)
        {
            options.Add("server=", "The Octopus server - e.g., 'http://octopus'", s => server = s);
            options.Add("apiKey=", "Your API key; you can get this from the Octopus web portal", s => apiKey = s);
            options.Add("u|username=", "If not using API keys, your username", s => username = s);
            options.Add("p|password=", "In not using API keys, your password", s => password = s);
        }

        public Uri ServerUri
        {
            get { return new Uri(server); }
        }

        public void Validate()
        {
            Guard.ArgumentNotNullOrEmpty(server, "Please specify an Octopus server, e.g., --server=http://your-octopus-server");

            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");
        }

        public async Task<IOctopusAsyncClient> CreateClient(IWebProxy overrideProxy)
        {
            IOctopusAsyncClient client = null;
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    var endpoint = new OctopusServerEndpoint(server);
                    if (overrideProxy != null)
                    {
                        endpoint.Proxy = overrideProxy;
                    }
                    client = await OctopusAsyncClient.Create(endpoint).ConfigureAwait(false);
                    await new OctopusAsyncRepository(client)
                        .Users
                        .SignIn(new LoginCommand { Username = username, Password = password });
                }
                else
                {
                    var endpoint = new OctopusServerEndpoint(server, apiKey, credentials: null);
                    if (overrideProxy != null)
                    {
                        endpoint.Proxy = overrideProxy;
                    }
                    client = await OctopusAsyncClient.Create(endpoint).ConfigureAwait(false);
                }
                return client;
            }
            catch (Exception)
            {
                client?.Dispose();
                throw;
            }
        }
    }
}