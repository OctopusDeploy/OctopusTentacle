using System;
using System.Net;
using System.Security.Cryptography;
using Autofac;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration, IStartable
    {
        readonly IKeyValueStore settings;

        public ProxyConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool UseDefaultProxy
        {
            get { return settings.Get("Octopus.Proxy.UseDefaultProxy", true); }
            set { settings.Set("Octopus.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return settings.Get("Octopus.Proxy.ProxyUsername", string.Empty); }
            set { settings.Set("Octopus.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return settings.Get("Octopus.Proxy.ProxyPassword", protectionScope: DataProtectionScope.LocalMachine); }
            set { settings.Set("Octopus.Proxy.ProxyPassword", value, DataProtectionScope.LocalMachine); }
        }

        public void Start()
        {
            try
            {
                if (UseDefaultProxy)
                {
                    var defaultProxy = WebRequest.GetSystemWebProxy();
                    defaultProxy.Credentials = string.IsNullOrWhiteSpace(CustomProxyUsername) 
                        ? CredentialCache.DefaultNetworkCredentials 
                        : new NetworkCredential(CustomProxyUsername, CustomProxyPassword);

                    WebRequest.DefaultWebProxy = defaultProxy;
                }
                else
                {
                    WebRequest.DefaultWebProxy = null;
                }
            }
            catch (Exception ex)
            {
                Log.Octopus().Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }
    }
}