using System;
using System.Linq;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class ApiEndpointOptions : ICommandOptions
    {
        const string ServerAddressNotSpecifiedMessage = "Please specify an Octopus Server, e.g., --server=http://your-octopus-server";

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

        public Uri ServerUri => Server != null ? new Uri(Server) : throw new ControlledFailureException(ServerAddressNotSpecifiedMessage);

        public bool IsSupplied => !string.IsNullOrWhiteSpace(Server) && (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(BearerToken));

        public void Validate()
        {
            var isServerSet = !string.IsNullOrEmpty(Server);
            var isBearerTokenSet = !string.IsNullOrEmpty(BearerToken);
            var isApiKeySet = !string.IsNullOrEmpty(ApiKey);
            var isUsernameSet = !string.IsNullOrEmpty(Username);
            var isPasswordSet = !string.IsNullOrEmpty(Password);

            var multipleCredentialsSet = new[] { isBearerTokenSet, isApiKeySet, isUsernameSet || isPasswordSet }.Count(x => x == true);
            if (multipleCredentialsSet >= 2)
                throw new ControlledFailureException("Please specify a Bearer Token, API Key or username and password - not multiple.");

            if (isUsernameSet && !isPasswordSet)
                throw new ControlledFailureException("Please specify a password for the specified user account");

            if (!isUsernameSet && isPasswordSet)
                throw new ControlledFailureException("Please specify a username for the specified password");

            const string credentialNotSpecifiedMessage = "Please specify an Octopus API key, a Bearer Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234";

            if (Optional)
            {
                if (isServerSet &&
                    !isBearerTokenSet &&
                    !isUsernameSet &&
                    !isApiKeySet)
                    throw new ControlledFailureException(credentialNotSpecifiedMessage);

                if (!isServerSet &&
                    (isBearerTokenSet || isUsernameSet || isApiKeySet))
                    throw new ControlledFailureException(ServerAddressNotSpecifiedMessage);
                return;
            }

            if (!isServerSet)
                throw new ControlledFailureException(ServerAddressNotSpecifiedMessage);

            if (!isBearerTokenSet &&
                !isUsernameSet &&
                !isApiKeySet)
                throw new ControlledFailureException(credentialNotSpecifiedMessage);
        }
    }
}