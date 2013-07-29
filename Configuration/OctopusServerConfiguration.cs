using System;
using Octopus.Client.Model;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Describes an Octopus server that the Tentacle communicates with.
    /// </summary>
    public class OctopusServerConfiguration
    {
        /// <summary>
        /// Create a new OctopusServerConfiguration.
        /// </summary>
        /// <param name="thumbprint"></param>
        public OctopusServerConfiguration(string thumbprint)
        {
            Thumbprint = thumbprint;
        }

        /// <summary>
        /// The server's X509 certificate thumbprint.
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// The communication style used with this server.
        /// </summary>
        public CommunicationStyle CommunicationStyle { get; set; }

        /// <summary>
        /// The URL used when connecting to the server, if available.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The server's unique identifier.
        /// </summary>
        public string Squid { get; set; }
    }
}