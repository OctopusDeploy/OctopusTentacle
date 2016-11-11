using System;
using System.Net;
using Autofac.Core;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration
{
    public class ProxyInitializer : IStartableOnRun
    {
        readonly IProxyConfiguration proxyConfiguration;
        readonly IProxyConfigParser configParser;

        public ProxyInitializer(IProxyConfiguration proxyConfiguration, IProxyConfigParser configParser)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.configParser = configParser;
        }

        public void Start()
        {
            InitializeProxy();
        }

        void InitializeProxy()
        {
            try
            {
                var config = proxyConfiguration;
                WebRequest.DefaultWebProxy = configParser.ParseToWebProxy(config);
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
    }
}