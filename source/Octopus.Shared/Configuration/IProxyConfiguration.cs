
namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Octopus and Tentacle machine-wide proxy settings (backed by the Windows Registry).
    /// </summary>
    public interface IProxyConfiguration
    {
        /// <summary>
        /// Gets or sets a flag indicating whether to use the a proxy.
        /// </summary>
        bool UseDefaultProxy { get; set; }

        /// <summary>
        /// Gets or sets a custom username for the proxy. If empty, we should assume to use the default Windows network
        /// credentials if <see cref="UseDefaultProxy" /> is true.
        /// </summary>
        string? CustomProxyUsername { get; set; }

        /// <summary>
        /// Gets or sets the password to go with <see cref="CustomProxyUsername" />.
        /// </summary>
        string? CustomProxyPassword { get; set; }

        /// <summary>
        /// Gets or sets the host use when overriding the default proxy. Leave empty to use the default proxy configured in IE.
        /// </summary>
        string? CustomProxyHost { get; set; }

        /// <summary>
        /// Gets or sets the port use when overriding the default proxy configured in IE.
        /// </summary>
        int CustomProxyPort { get; set; }
    }

    public static class ProxyConfigurationExtensions
    {
        public static bool ProxyEnabled(this IProxyConfiguration config)
        {
            return config.UseDefaultProxy || config.UsingCustomProxy();
        }

        public static bool ProxyDisabled(this IProxyConfiguration config)
        {
            return !config.ProxyEnabled();
        }

        public static bool UsingCustomProxy(this IProxyConfiguration config)
        {
            return !string.IsNullOrWhiteSpace(config.CustomProxyHost);
        }

        public static bool UsingDefaultCredentials(this IProxyConfiguration config)
        {
            return string.IsNullOrWhiteSpace(config.CustomProxyUsername);
        }
    }
}