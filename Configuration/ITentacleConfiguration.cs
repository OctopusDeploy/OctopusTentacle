using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Tentacle configuration settings (backed by the Windows Registry).
    /// </summary>
    public interface ITentacleConfiguration : IModifiableConfiguraton
    {
        /// <summary>
        /// Gets the list of Octopus servers that this Tentacle communicates with.
        /// </summary>
        IEnumerable<OctopusServerConfiguration> TrustedOctopusServers { get; }

        /// <summary>
        /// Gets the list of X509 certificate thumbprints trusted by the tentacle.
        /// </summary>
        IEnumerable<string> TrustedOctopusThumbprints { get; }

        /// <summary>
        /// Adds a trusted Octopus server.
        /// </summary>
        /// <param name="machine">Configuration for the server.</param>
        void AddTrustedOctopusServer(OctopusServerConfiguration machine);

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

        /// <summary>
        /// Gets or sets the TCP port number used by the Tentacle WCF services (default is 10933).
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
        /// Gets the directory to which job logs will be uploaded to on this machine.
        /// </summary>
        string LogsDirectory { get; }

        /// <summary>
        /// Gets the file where deployment entries should be added.
        /// </summary>
        string JournalFilePath { get; }

        /// <summary>
        /// Gets or sets the X509 certificate used by the Tentacle.
        /// </summary>
        X509Certificate2 TentacleCertificate { get; set; }

        /// <summary>
        /// Gets or sets the IP address/hostname used to listen on (by default, localhost).
        /// </summary>
        string ServicesHostName { get; set; }

        /// <summary>
        /// Gets or sets the SQUID uniquely identifying the local instance. This value must be unique
        /// among all entities in an Octopus network.
        /// </summary>
        string Squid { get; set; }
    }
}