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
        readonly IProxyConfigParser configParser;

        public ProxyInitializer(Lazy<IProxyConfiguration> proxyConfiguration, IApplicationInstanceSelector selector, IProxyConfigParser configParser)
        {
            this.proxyConfiguration = proxyConfiguration;
            this.selector = selector;
            this.configParser = configParser;
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
                WebRequest.DefaultWebProxy = configParser.ParseToWebProxy(proxyConfiguration.Value);
            }
            catch (Exception ex)
            {
                Log.Octopus().Warn(ex, "Unable to configure the proxy server: " + ex.Message);
            }
        }
    }
}