using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Configures the local instance when acting as a TCP server.
    /// </summary>
    public interface ITcpServerCommunicationsConfiguration
    {
        /// <summary>
        /// The port on which the instance will listen for incoming connections. The default is 10943.
        /// </summary>
        int ServicesPort { get; }
    }
}