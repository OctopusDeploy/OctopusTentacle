using System;
using System.Net;

namespace Octopus.Shared.Configuration
{
    public interface IWebPortalConfiguration
    {
        /// <summary>
        /// Gets or sets a comma-seperated list of <see cref="HttpListener"/> prefixes that the web server should listen on.
        /// </summary>
        string ListenPrefixes { get; set; }

        /// <summary>
        /// Gets or sets whether SSL will be forced (connections to any non-HTTPS prefix will be redirected to the first HTTPS prefix).
        /// </summary>
        bool ForceSsl { get; set; }

        /// <summary>
        /// Gets or sets which authentication mode to use.
        /// </summary>
        AuthenticationMode AuthenticationMode { get; set; }
    }
}