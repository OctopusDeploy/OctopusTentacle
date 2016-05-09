using System;
using Halibut;
using Halibut.Transport.Proxy;
using Octopus.Client;

namespace Octopus.Shared.Util
{
    public static class ProxyExtensions
    {
        public static ProxyDetails ToHalibutProxy(this OctopusProxyConfiguration proxy)
        {
            return proxy == null
                ? null
                : new ProxyDetails(proxy.Host, proxy.Port, proxy.Type.ToHalibutProxyType(), proxy.Username, proxy.Password);
        }

        public static ProxyType ToHalibutProxyType(this OctopusProxyType type)
        {
            switch (type)
            {
                case OctopusProxyType.HTTP:
                    return ProxyType.HTTP;
                case OctopusProxyType.SOCKS4:
                    return ProxyType.SOCKS4;
                case OctopusProxyType.SOCKS4a:
                    return ProxyType.SOCKS4A;
                case OctopusProxyType.SOCKS5:
                    return ProxyType.SOCKS5;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}