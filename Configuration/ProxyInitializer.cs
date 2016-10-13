using System;
using System.Net;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ProxyInitializer : IStartable
    {
        readonly Lazy<IProxyConfiguration> proxyConfiguration;
        readonly IProxyConfigParser configParser;

        public ProxyInitializer(Lazy<IProxyConfiguration> proxyConfiguration, IProxyConfigParser configParser)
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
                var config = proxyConfiguration.Value;
                WebRequest.DefaultWebProxy = configParser.ParseToWebProxy(config);
            }
            catch (Exception ex)
            {
                Log.Octopus().Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }
    }
}