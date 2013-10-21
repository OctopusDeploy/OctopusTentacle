using System;
using System.IO;
using System.Net;
using Octopus.Platform.Deployment.Configuration;

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
        /// Gets or sets whether SSL will be forced (connections to any non-HTTPS prefix will be redirected to the first HTTPS prefix).
        /// </summary>
        public bool ForceSsl
        {
            get { return settings.Get("Octopus.WebPortal.ForceSsl", false); }
            set { settings.Set("Octopus.WebPortal.ForceSsl", value); }
        }

        /// <summary>
        /// Gets or sets which authentication mode to use.
        /// </summary>
        public AuthenticationMode AuthenticationMode
        {
            get { return settings.Get("Octopus.WebPortal.AuthenticationMode", AuthenticationMode.UsernamePassword); }
            set { settings.Set("Octopus.WebPortal.AuthenticationMode", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}