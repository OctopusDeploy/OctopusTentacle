using System;
using System.Net;
using Octopus.Client.Model;

namespace Octopus.Shared.Configuration
{
    public interface IWebPortalConfiguration : IModifiableConfiguration
    {
        /// <summary>
        /// Gets or sets a comma-seperated list of <see cref="HttpListener" /> prefixes that the web server should listen on.
        /// </summary>
        string ListenPrefixes { get; set; }

        /// <summary>
        /// Gets or sets whether SSL will be forced (connections to any non-HTTPS prefix will be redirected to the first HTTPS
        /// prefix).
        /// </summary>
        bool ForceSsl { get; set; }

        /// <summary>
        /// Gets or sets whether guest login is enabled.
        /// </summary>
        bool GuestLoginEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether HTTP request logging is enabled.
        /// </summary>
        bool RequestLoggingEnabled { get; set; }

        /// <summary>
        /// Gets or sets which authentication mode to use.
        /// </summary>
        AuthenticationMode AuthenticationMode { get; set; }

        /// <summary>
        /// Gets or sets the active directory container, if not specified default container is used
        /// </summary>
        string ActiveDirectoryContainer { get; set; }

        /// <summary>
        /// Gets or sets an optional whitelist of allowed domains (empty or null turns CORS off)
        /// </summary>
        string CorsWhitelist { get; set; }

        /// <summary>
        /// Gets or sets a uri to be provided in the X-Frame-Options header ALLOW-FROM uri. The absence of a uri implies DENY will be used.
        /// </summary>
        string XFrameOptionAllowFrom { get; set; }

        /// <summary>
        /// Gets or sets the authentication scheme to use when authentication Domain users.
        /// </summary>
        AuthenticationSchemes AuthenticationScheme { get; set; }

        /// <summary>
        /// Gets or sets the when the HTML-based username/password form will be presented for domain users. Defaults to true. 
        /// </summary>
        bool AllowFormsAuthenticationForDomainUsers { get; set; }

        bool IsFormsAuthAllowed();

        bool IsExternalLoginEnabled();
    }
}