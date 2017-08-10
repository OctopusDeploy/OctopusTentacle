using System;
using System.Net;
using Autofac.Core;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ProxyInitializer : IProxyInitializer
    {
        readonly IProxyConfiguration proxyConfiguration;
        readonly IProxyConfigParser configParser;

        public ProxyInitializer(IProxyConfiguration proxyConfiguration, IProxyConfigParser configParser)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.configParser = configParser;
        }

        public void InitializeProxy()
        {
            try
            {
                var proxy = configParser.ParseToWebProxy(proxyConfiguration);
                WebRequest.DefaultWebProxy = proxy;
            }
            catch (DependencyResolutionException dre) when (dre.InnerException is ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Octopus().Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }

        public IWebProxy GetProxy()
        {
            return WebRequest.DefaultWebProxy;
        }
    }
}