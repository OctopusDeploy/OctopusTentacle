using System;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class ApiEndpointOptions : ICommandOptions
    {
        public string Server { get; private set; } = null!;
        public string ApiKey { get; private set; } = null!;
        public string Token { get; private set; } = null!;
        public string Username { get; private set; } = null!;
        public string Password { get; private set; } = null!;

        public bool IgnoreSslErrors { get; private set; } = false;
        public bool Optional { private get; set; }

        public ApiEndpointOptions(OptionSet options)
        {
            options.Add("server=", "The Octopus Server - e.g., 'http://octopus'", s => Server = s);
            options.Add("apiKey=", "Your API key; you can get this from the Octopus web portal", s => ApiKey = s, sensitive: true);
            options.Add("token=", "A JWT Token which has access to your Octopus instance", t => Token = t, sensitive: true);
            options.Add("u|username=|user=", "If not using API keys, your username", s => Username = s);
            options.Add("p|password=", "If not using API keys, your password", s => Password = s, sensitive: true);
#if HTTP_CLIENT_SUPPORTS_SSL_OPTIONS
            options.Add("ignoreSslErrors", "Set this flag if your Octopus Server uses HTTPS but the certificate is not trusted on this machine. Any certificate errors will be ignored. WARNING: this option may create a security vulnerability.", v => IgnoreSslErrors = true);
#endif
        }

        public Uri? ServerUri => Server != null ? new Uri(Server) : null;

        public bool IsSupplied => !string.IsNullOrWhiteSpace(Server) && (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(Token));

        public void Validate()
        {
            if (!string.IsNullOrEmpty(Token) &&
                !string.IsNullOrEmpty(ApiKey) &&
                (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password)))
                throw new ControlledFailureException("Please specify a Token, API Key or username and password - not multiple.");

            if (!string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password))
                throw new ControlledFailureException("Please specify a password for the specified user account");

            if (string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                throw new ControlledFailureException("Please specify a username for the specified password");

            if (Optional)
            {
                if (!string.IsNullOrEmpty(Server) &&
                    string.IsNullOrEmpty(Token) &&
                    string.IsNullOrEmpty(Username) &&
                    string.IsNullOrEmpty(ApiKey))
                    throw new ControlledFailureException("Please specify an Octopus API key, a Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");

                if (string.IsNullOrEmpty(Server) &&
                    (!string.IsNullOrEmpty(Token) || !string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(ApiKey)))
                    throw new ControlledFailureException("Please specify an Octopus Server, e.g., --server=http://your-octopus-server");
                return;
            }

            if (string.IsNullOrWhiteSpace(Server))
                throw new ControlledFailureException("Please specify an Octopus Server, e.g., --server=http://your-octopus-server");

            if (string.IsNullOrEmpty(Username) &&
                string.IsNullOrEmpty(ApiKey) &&
                string.IsNullOrEmpty(Token))
                throw new ControlledFailureException("Please specify an Octopus API key, a Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");
        }
    }
}
