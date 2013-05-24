using System;
using System.Net;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ProxyConfiguration : IProxyConfiguration, IStartable
    {
        readonly IWindowsRegistry registry;
        
        public ProxyConfiguration(IWindowsRegistry registry)
        {
            this.registry = registry;
        }

        public bool UseDefaultProxy
        {
            get { return registry.Get("Octopus.Proxy.UseDefaultProxy", true); }
            set { registry.Set("Octopus.Proxy.UseDefaultProxy", value); }
        }

        public string CustomProxyUsername
        {
            get { return registry.Get("Octopus.Proxy.ProxyUsername", string.Empty); }
            set { registry.Set("Octopus.Proxy.ProxyUsername", value); }
        }

        public string CustomProxyPassword
        {
            get { return registry.GetSecure("Octopus.Proxy.ProxyPassword"); }
            set { registry.SetSecure("Octopus.Proxy.ProxyPassword", value); }
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
                LogAdapter.GetDefault().Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }
    }
}