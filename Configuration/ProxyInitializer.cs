using System;
using System.Net;
using Autofac;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ProxyInitializer : IStartable
    {
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly IApplicationInstanceSelector selector;

        public ProxyInitializer(Lazy<IProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector selector)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.selector = selector;
        }

        public void Start()
        {
            selector.Loaded += InitializeProxy;
            InitializeProxy();
        }

        void InitializeProxy()
        {
            if (selector.Current == null)
                return;

            selector.Loaded -= InitializeProxy;
            
            try
            {
                if (proxyConfiguration.Value.UseDefaultProxy)
                {
                    var defaultProxy = WebRequest.GetSystemWebProxy();
                    defaultProxy.Credentials = string.IsNullOrWhiteSpace(proxyConfiguration.Value.CustomProxyUsername)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(proxyConfiguration.Value.CustomProxyUsername, proxyConfiguration.Value.CustomProxyPassword);

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