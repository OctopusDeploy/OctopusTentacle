using System;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Tentacle configuration settings (backed by the Windows Registry).
    /// </summary>
    public interface ITentacleConfiguration : IModifiableConfiguraton
    {
        /// <summary>
        /// Gets or sets the list of X509 certificate thumbprints of Octopus servers that this Tentacle trusts.
        /// </summary>
        string[] TrustedOctopusThumbprints { get; set; }

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
    }
}