using System;
using System.Net;
using Halibut;
using Halibut.Transport.Proxy;
using Octopus.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public interface IProxyConfigParser
    {
        ProxyDetails ParseToHalibutProxy(IProxyConfiguration config, Uri destination, ILog log);
        IWebProxy ParseToWebProxy(IProxyConfiguration config);
    }

    public class ProxyConfigParser : IProxyConfigParser
    {
        public Func<IWebProxy> GetSystemWebProxy = WebRequest.GetSystemWebProxy; //allow us to swap this for tests without having to inject

        public ProxyDetails ParseToHalibutProxy(IProxyConfiguration config, Uri destination, ILog log)
        {
            if (config == null)
                return null;

            if (config.UseDefaultProxy)
            {
                var proxy = GetSystemWebProxy();

                if (proxy.IsBypassed(destination))
                {
                    log.Info($"Agent configured to use the system proxy, but no system proxy is configured for {destination}");
                    return null;
                }

                var address = proxy.GetProxy(destination);
                log.Info($"Agent will use the configured system proxy at {address.Host}:{address.Port} for server at {destination}");
                return config.UsingDefaultCredentials()
                    ? new ProxyDetails(address.Host, address.Port, ProxyType.HTTP, CredentialCache.DefaultNetworkCredentials.UserName, CredentialCache.DefaultNetworkCredentials.Password)
                    : new ProxyDetails(address.Host, address.Port, ProxyType.HTTP, config.CustomProxyUsername, config.CustomProxyPassword);
            }

            if(config.UsingCustomProxy())
            {
                log.Info($"Agent will use the octopus configured proxy at {config.CustomProxyHost}:{config.CustomProxyPort} for server at {destination}");
                return string.IsNullOrWhiteSpace(config.CustomProxyUsername)
                    ? new ProxyDetails(config.CustomProxyHost, config.CustomProxyPort, ProxyType.HTTP, null, null) //Don't use default creds for custom proxy if user has not supplied any
                    : new ProxyDetails(config.CustomProxyHost, config.CustomProxyPort, ProxyType.HTTP, config.CustomProxyUsername, config.CustomProxyPassword);
            }

            log.Info("Agent will not use a proxy server");
            return null;
        }

        public IWebProxy ParseToWebProxy(IProxyConfiguration config)
        {
            if (config == null)
                return null;

            if (config.UseDefaultProxy)
            {
                var proxy = GetSystemWebProxy();

                proxy.Credentials = config.UsingDefaultCredentials()
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(config.CustomProxyUsername, config.CustomProxyPassword);

                return proxy;
            }

            if (config.UsingCustomProxy())
            {
                var proxy = new WebProxy(new UriBuilder("http", config.CustomProxyHost, config.CustomProxyPort).Uri);

                proxy.Credentials = config.UsingDefaultCredentials()
                    ? new NetworkCredential()
                    : new NetworkCredential(config.CustomProxyUsername, config.CustomProxyPassword);

                return proxy;
            }

            return null;
        }
    }
}