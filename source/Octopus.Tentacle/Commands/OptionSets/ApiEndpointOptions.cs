using System;
using System.Linq;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class ApiEndpointOptions : ICommandOptions
    {
        public string Server { get; private set; } = null!;
        public string ApiKey { get; private set; } = null!;
        public string BearerToken { get; private set; } = null!;
        public string Username { get; private set; } = null!;
        public string Password { get; private set; } = null!;

        public bool IgnoreSslErrors { get; private set; } = false;

        public bool Optional { private get; set; }

        public ApiEndpointOptions(OptionSet options)
        {
            options.Add("server=", "The Octopus Server - e.g., 'http://octopus'", s => Server = s);
            options.Add("apiKey=", "Your API key; you can get this from the Octopus web portal", s => ApiKey = s, sensitive: true);
            options.Add("bearerToken=", "A Bearer Token which has access to your Octopus instance", t => BearerToken = t, sensitive: true);
            options.Add("u|username=|user=", "If not using API keys, your username", s => Username = s);
            options.Add("p|password=", "If not using API keys, your password", s => Password = s, sensitive: true);
#if HTTP_CLIENT_SUPPORTS_SSL_OPTIONS
            options.Add("ignoreSslErrors", "Set this flag if your Octopus Server uses HTTPS but the certificate is not trusted on this machine. Any certificate errors will be ignored. WARNING: this option may create a security vulnerability.", v => IgnoreSslErrors = true);
#endif
        }

        public Uri? ServerUri => Server != null ? new Uri(Server) : null;

        public bool IsSupplied => !string.IsNullOrWhiteSpace(Server) && (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(BearerToken));

        public void Validate()
        {
            var serverSet = !string.IsNullOrEmpty(Server);
            var bearerTokenSet = !string.IsNullOrEmpty(BearerToken);
            var apiKeySet = !string.IsNullOrEmpty(ApiKey);
            var usernameSet = !string.IsNullOrEmpty(Username);
            var password = !string.IsNullOrEmpty(Password);
            var multipleCredentialsSet = new[] { bearerTokenSet, apiKeySet, usernameSet || password }.Count(x => x == true);
            if (multipleCredentialsSet >= 2)
                throw new ControlledFailureException("Please specify a Bearer Token, API Key or username and password - not multiple.");

            if (usernameSet && !password)
                throw new ControlledFailureException("Please specify a password for the specified user account");

            if (!usernameSet && password)
                throw new ControlledFailureException("Please specify a username for the specified password");

            if (Optional)
            {
                if (serverSet &&
                    !bearerTokenSet &&
                    !usernameSet &&
                    !apiKeySet)
                    throw new ControlledFailureException("Please specify an Octopus API key, a Bearer Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");

                if (!serverSet &&
                    (bearerTokenSet || usernameSet || apiKeySet))
                    throw new ControlledFailureException("Please specify an Octopus Server, e.g., --server=http://your-octopus-server");
                return;
            }

            if (!serverSet)
                throw new ControlledFailureException("Please specify an Octopus Server, e.g., --server=http://your-octopus-server");

            if (!usernameSet &&
                !apiKeySet &&
                !bearerTokenSet)
                throw new ControlledFailureException("Please specify an Octopus API key, a Bearer Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");
        }
    }
}