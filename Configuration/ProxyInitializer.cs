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
                if (proxyConfiguration.Value.UseDefaultProxy)
                {
                    var proxy = string.IsNullOrWhiteSpace(proxyConfiguration.Value.CustomProxyHost)
                        ? WebRequest.GetSystemWebProxy()
                        : BuildCustomProxy(proxyConfiguration.Value.CustomProxyHost, proxyConfiguration.Value.CustomProxyPort);


                    proxy.Credentials = string.IsNullOrWhiteSpace(proxyConfiguration.Value.CustomProxyUsername)
                        ? CredentialCache.DefaultNetworkCredentials
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

        public static IWebProxy BuildCustomProxy(string hostname, int port)
        {
            var url = hostname;
            if (!(url.StartsWith("http://") || url.StartsWith("https://")))
            {
                url = "http://" + url; //we don't use the http:// but Uri ctor needs it
            }

            if (!hostname.Replace("://", "").Contains(":"))
            {
                url = url + ":" + port;
            }

            return new WebProxy(new Uri(url));
        }
    }
}