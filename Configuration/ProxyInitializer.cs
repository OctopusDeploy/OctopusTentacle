using System;
using System.Net;
using System.Security.Policy;
using Autofac;
using Octopus.Shared.Diagnostics;

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
                var useCustomProxy = proxyConfiguration.Value.UsingCustomProxy();
                var useDefaultProxy = proxyConfiguration.Value.UseDefaultProxy;

                if (useDefaultProxy || useCustomProxy)
                {
                    var proxy = useDefaultProxy
                        ? WebRequest.GetSystemWebProxy()
                        : new WebProxy(new UriBuilder("http", proxyConfiguration.Value.CustomProxyHost, proxyConfiguration.Value.CustomProxyPort).Uri);

                    var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyConfiguration.Value.CustomProxyUsername);

                    proxy.Credentials = useDefaultCredentials
                        ? useDefaultProxy
                            ? CredentialCache.DefaultNetworkCredentials
                            : new NetworkCredential()
                        : new NetworkCredential(proxyConfiguration.Value.CustomProxyUsername, proxyConfiguration.Value.CustomProxyPassword);

                    WebRequest.DefaultWebProxy = proxy;
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