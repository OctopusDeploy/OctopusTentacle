using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Octopus and Tentacle machine-wide proxy settings (backed by the Windows Registry).
    /// </summary>
    public interface IProxyConfiguration : IModifiableConfiguration
    {
        /// <summary>
        /// Gets or sets a flag indicating whether to use the a proxy.
        /// </summary>
        bool UseDefaultProxy { get; set; }

        /// <summary>
        /// Gets or sets a custom username for the proxy. If empty, we should assume to use the default Windows network
        /// credentials if <see cref="UseDefaultProxy" /> is true.
        /// </summary>
        string CustomProxyUsername { get; set; }

        /// <summary>
        /// Gets or sets the password to go with <see cref="CustomProxyUsername" />.
        /// </summary>
        string CustomProxyPassword { get; set; }

        /// <summary>
        /// Gets or sets the host use when overriding the default proxy. Leave empty to use the default proxy configured in IE.
        /// </summary>
        string CustomProxyHost { get; set; }

        /// <summary>
        /// Gets or sets the port use when overriding the default proxy configured in IE.
        /// </summary>
        int CustomProxyPort { get; set; }
    }
}