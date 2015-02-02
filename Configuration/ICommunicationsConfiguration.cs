using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Configures how the local instance communicates with other instances.
    /// </summary>
    public interface ICommunicationsConfiguration : IModifiableConfiguration
    {
        /// <summary>
        /// Gets the directory in which file streams are stored.
        /// </summary>
        string StreamsDirectory { get; }

        /// <summary>
        /// The port on which incoming communications will be received.
        /// </summary>
        int ServicesPort { get; set; }
    }
}
