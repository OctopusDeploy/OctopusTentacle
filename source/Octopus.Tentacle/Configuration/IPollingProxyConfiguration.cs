using System;

namespace Octopus.Tentacle.Configuration
{
    /// <summary>
    /// Tentacle settings for the proxy that is used to communicate with Octopus.
    /// </summary>
    public interface IPollingProxyConfiguration : IProxyConfiguration
    {
    }

    public interface IWritablePollingProxyConfiguration : IWritableProxyConfiguration
    {
    }
}