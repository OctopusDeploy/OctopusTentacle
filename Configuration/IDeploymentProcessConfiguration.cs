using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Octopus configuration settings.
    /// </summary>
    public interface IDeploymentProcessConfiguration
    {
        /// <summary>
        /// Gets the directory that Octopus Server should use to store downloaded packages.
        /// </summary>
        string CacheDirectory { get; }
    }
}