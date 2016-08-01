using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Octopus.Server.Extensibility.Configuration;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Tentacle configuration settings (backed by the Windows Registry).
    /// </summary>
    public interface ITentacleConfiguration : IModifiableConfiguration
    {

        /// <summary>
        /// Gets the Squid for this tentacle.
        /// </summary>
        [Obsolete("This configuration entry is obsolete as of 3.0. It is only used as a Subscription ID where one does not exist.")]
        string TentacleSquid { get; }

        /// <summary>
        /// Gets the list of Octopus servers that this Tentacle communicates with.
        /// </summary>
        IEnumerable<OctopusServerConfiguration> TrustedOctopusServers { get; }

        /// <summary>
        /// Gets the list of X509 certificate thumbprints trusted by the tentacle.
        /// </summary>
        IEnumerable<string> TrustedOctopusThumbprints { get; }

        /// <summary>
        /// Gets or sets the TCP port number used by the Tentacle distribution service (default is 10933).
        /// </summary>
        int ServicesPortNumber { get; set; }

        /// <summary>
        /// Gets or sets the directory in which NuGet packages will be installed on this machine.
        /// </summary>
        string ApplicationDirectory { get; set; }

        /// <summary>
        /// Gets the directory in which NuGet packages will be uploaded to on this machine.
        /// </summary>
        string PackagesDirectory { get; }

        /// <summary>
        /// Gets the file where deployment entries should be added.
        /// </summary>
        string JournalFilePath { get; }

        /// <summary>
        /// Gets or sets the X509 certificate used by the Tentacle.
        /// </summary>
        X509Certificate2 TentacleCertificate { get; }

        /// <summary>
        /// Gets or sets the IP address to listen on.
        /// </summary>
        string ListenIpAddress { get; set; }

        /// <summary>
        /// Even in polling mode, by default Tentacle will listen on a TCP port for connections, just in case you
        /// also want it to be a listening Tentacle. Set this flag to true to stop Tentacle listening on a port.
        /// </summary>
        bool NoListen { get; set; }

        /// <summary>
        /// The details received in the most recent handshake request.
        /// </summary>
        OctopusServerConfiguration LastReceivedHandshake { get; set; }

        /// <summary>
        /// Adds a trusted Octopus server.
        /// </summary>
        /// <param name="machine">Configuration for the server.</param>
        bool AddOrUpdateTrustedOctopusServer(OctopusServerConfiguration machine);

        /// <summary>
        /// Remove all trusted Octopus servers.
        /// </summary>
        void ResetTrustedOctopusServers();

        /// <summary>
        /// Remove the trusted server with the matching thumbprint, if any.
        /// </summary>
        /// <param name="toRemove">Thumbprint to remove.</param>
        void RemoveTrustedOctopusServersWithThumbprint(string toRemove);

        /// <summary>
        /// Update the thumbprint of an existing server.
        /// </summary>
        /// <param name="old">The old thumbprint.</param>
        /// <param name="new">The new one.</param>
        void UpdateTrustedServerThumbprint(string old, string @new);

        X509Certificate2 GenerateNewCertificate();
        void ImportCertificate(X509Certificate2 certificate);

        /// <summary>
        /// Gets the proxy used for communications.
        /// </summary>
        IProxyConfiguration ProxyConfiguration { get; }

        /// <summary>
        /// Gets the proxy used for halibut communications with the octopus server.
        /// </summary>
        IPollingProxyConfiguration PollingProxyConfiguration { get; }

    }
}