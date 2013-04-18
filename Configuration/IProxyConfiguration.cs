using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Octopus and Tentacle machine-wide proxy settings (backed by the Windows Registry).
    /// </summary>
    public interface IProxyConfiguration
    {
        /// <summary>
        /// Gets or sets a flag indicating whether to use the default proxy as configured in IE.
        /// </summary>
        bool UseDefaultProxy { get; set; }

        /// <summary>
        /// Gets or sets a custom username for the proxy. If empty, we should assume to use the default Windows network credentials if <see cref="UseDefaultProxy"/> is true.
        /// </summary>
        string CustomProxyUsername { get; set; }

        /// <summary>
        /// Gets or sets the password to go with <see cref="CustomProxyUsername"/>. 
        /// </summary>
        string CustomProxyPassword { get; set; }
    }
}