
namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Octopus and Tentacle machine-wide proxy settings (backed by the Windows Registry).
    /// </summary>
    public interface IProxyConfiguration
    {
        /// <summary>
        /// Gets a flag indicating whether to use the a proxy.
        /// </summary>
        bool UseDefaultProxy { get; }

        /// <summary>
        /// Gets a custom username for the proxy. If empty, we should assume to use the default Windows network
        /// credentials if <see cref="UseDefaultProxy" /> is true.
        /// </summary>
        string? CustomProxyUsername { get; }

        /// <summary>
        /// Gets the password to go with <see cref="CustomProxyUsername" />.
        /// </summary>
        string? CustomProxyPassword { get; }

        /// <summary>
        /// Gets the host use when overriding the default proxy. Leave empty to use the default proxy configured in IE.
        /// </summary>
        string? CustomProxyHost { get; }

        /// <summary>
        /// Gets the port use when overriding the default proxy configured in IE.
        /// </summary>
        int CustomProxyPort { get; }
    }

    public interface IModifiableProxyConfiguration : IProxyConfiguration
    {
        void SetUseDefaultProxy(bool useDefaultProxy);

        void SetCustomProxyUsername(string? username);

        void SetCustomProxyPassword(string? password);

        void SetCustomProxyHost(string? host);

        void SetCustomProxyPort(int? port);

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