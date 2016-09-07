using System;
using Octopus.Server.Extensibility.HostServices.Configuration;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Configures how the local instance communicates with other instances.
    /// </summary>
    public interface ICommunicationsConfiguration : IModifiableConfiguration
    {
        /// <summary>
        /// The port on which incoming communications will be received.
        /// </summary>
        int ServicesPort { get; set; }
    }
}