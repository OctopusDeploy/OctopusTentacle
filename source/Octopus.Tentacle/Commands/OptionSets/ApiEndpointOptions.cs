using System;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Commands.OptionSets
{
    public class ApiEndpointOptions : ICommandOptions
    {
        public string Server { get; private set; }
        public string ApiKey { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public ApiEndpointOptions(OptionSet options)
        {
            options.Add("server=", "The Octopus server - e.g., 'http://octopus'", s => Server = s);
            options.Add("apiKey=", "Your API key; you can get this from the Octopus web portal", s => ApiKey = s);
            options.Add("u|username=", "If not using API keys, your username", s => Username = s);
            options.Add("p|password=", "In not using API keys, your password", s => Password = s);
        }

        public Uri ServerUri
        {
            get { return new Uri(Server); }
        }

        public void Validate()
        {
            Guard.ArgumentNotNullOrEmpty(Server, "Please specify an Octopus server, e.g., --server=http://your-octopus-server");

            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(ApiKey))
                throw new ArgumentException("Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234");
        }
    }
}