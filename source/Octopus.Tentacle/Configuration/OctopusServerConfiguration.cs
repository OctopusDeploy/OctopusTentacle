using System;
using Octopus.Client.Model;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Configuration
{
    /// <summary>
    /// Describes an Octopus server that the Tentacle communicates with.
    /// </summary>
    public class OctopusServerConfiguration
    {
        string thumbprint;

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
        public string Thumbprint
        {
            get { return thumbprint; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("A valid thumbprint must be supplied");

                thumbprint = value.Trim();
            }
        }

        /// <summary>
        /// The communication style used with this server.
        /// </summary>
        public CommunicationStyle CommunicationStyle { get; set; }

        /// <summary>
        /// The URL used when connecting to the server, if available.
        /// </summary>
        public Uri Address { get; set; }

        /// <summary>
        /// The server's unique identifier.
        /// </summary>
        public string Squid { get; set; }

        public string SubscriptionId { get; set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            return ObjectFormatter.Format(this);
        }
    }
}