using System;
using System.Net;
using Autofac.Core;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public class ProxyInitializer : IProxyInitializer
    {
        readonly IProxyConfiguration proxyConfiguration;
        readonly IProxyConfigParser configParser;
        readonly ISystemLog log;

        public ProxyInitializer(IProxyConfiguration proxyConfiguration, IProxyConfigParser configParser, ISystemLog log)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.configParser = configParser;
            this.log = log;
        }

        public void InitializeProxy()
        {
            try
            {
                var proxy = configParser.ParseToWebProxy(proxyConfiguration);
                var _ = WebRequest.DefaultWebProxy; // https://github.com/dotnet/corefx/issues/23886
                WebRequest.DefaultWebProxy = proxy;
            }
            catch (DependencyResolutionException dre) when (dre.InnerException is ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }

        public IWebProxy GetProxy()
            => WebRequest.DefaultWebProxy;
    }
}