using System;
using System.Net;
using Octopus.Client.Model;

namespace Octopus.Shared.Configuration
{
    public class WebPortalConfiguration : IWebPortalConfiguration
    {
        readonly IKeyValueStore settings;

        public WebPortalConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Gets or sets a comma-seperated list of <see cref="HttpListener" /> prefixes that the web server should listen on.
        /// </summary>
        public string ListenPrefixes
        {
            get { return settings.Get("Octopus.WebPortal.ListenPrefixes"); }
            set { settings.Set("Octopus.WebPortal.ListenPrefixes", value); }
        }

        /// <summary>
        /// Gets or sets whether SSL will be forced (connections to any non-HTTPS prefix will be redirected to the first HTTPS
        /// prefix).
        /// </summary>
        public bool ForceSsl
        {
            get { return settings.Get("Octopus.WebPortal.ForceSsl", false); }
            set { settings.Set("Octopus.WebPortal.ForceSsl", value); }
        }

        /// <summary>
        /// Gets or sets whether guest login is enabled.
        /// </summary>
        public bool GuestLoginEnabled
        {
            get { return settings.Get("Octopus.WebPortal.GuestLoginEnabled", false); }
            set { settings.Set("Octopus.WebPortal.GuestLoginEnabled", value); }
        }

        /// <summary>
        /// Gets or sets whether HTTP request logging is enabled.
        /// </summary>
        public bool RequestLoggingEnabled
        {
            get { return settings.Get("Octopus.WebPortal.RequestLoggingEnabled", false); }
            set { settings.Set("Octopus.WebPortal.RequestLoggingEnabled", value); }
        }

        /// <summary>
        /// Gets or sets which authentication mode to use.
        /// </summary>
        public AuthenticationMode AuthenticationMode
        {
            get { return settings.Get("Octopus.WebPortal.AuthenticationMode", AuthenticationMode.UsernamePassword); }
            set { settings.Set("Octopus.WebPortal.AuthenticationMode", value); }
        }

        /// <summary>
        /// Gets or sets the active directory container, if not specified default container is used
        /// </summary>
        public string ActiveDirectoryContainer
        {
            get { return settings.Get("Octopus.WebPortal.ActiveDirectoryContainer", String.Empty); }
            set { settings.Set("Octopus.WebPortal.ActiveDirectoryContainer", value); }
        }

        /// <summary>
        /// Gets or sets an optional whitelist of allowed domains (empty will disable CORS)
        /// </summary>
        public string CorsWhitelist
        {
            get { return settings.Get("Octopus.WebPortal.CorsWhitelist", string.Empty); }
            set { settings.Set("Octopus.WebPortal.CorsWhitelist", value); }
        }

        public string XFrameOptionAllowFrom
        {
            get { return settings.Get("Octopus.WebPortal.XFrameOptionAllowFrom", string.Empty); }
            set { settings.Set("Octopus.WebPortal.XFrameOptionAllowFrom", value); }
        }

        /// <summary>
        /// Gets or sets the authentication scheme to use when authentication Domain users.
        /// </summary>
        public AuthenticationSchemes AuthenticationScheme
        {
            get { return settings.Get("Octopus.WebPortal.AuthenticationScheme", AuthenticationSchemes.Ntlm); }
            set { settings.Set("Octopus.WebPortal.AuthenticationScheme", value); }
        }

        /// <summary>
        /// Gets or sets the when the HTML-based username/password form will be presented for domain users. Defaults to true. 
        /// </summary>
        public bool AllowFormsAuthenticationForDomainUsers
        {
            get { return settings.Get("Octopus.WebPortal.AllowFormsAuthenticationForDomainUsers", true); }
            set { settings.Set("Octopus.WebPortal.AllowFormsAuthenticationForDomainUsers", value); }
        }

        public bool IsFormsAuthAllowed()
        {
            return AuthenticationMode == AuthenticationMode.UsernamePassword || AllowFormsAuthenticationForDomainUsers;
        }

        public void Save()
        {
            settings.Save();
        }
    }
}