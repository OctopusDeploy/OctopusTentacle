using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Octopus configuration settings (backed by the Windows Registry). 
    /// </summary>
    public interface IOctopusConfiguration
    {
        /// <summary>
        /// Gets or sets whether Octopus should set up a default backup policy.
        /// </summary>
        bool BackupsEnabledByDefault { get; set; }

        /// <summary>
        /// Gets or sets where Octopus should store the RavenDB data on the Octopus server.
        /// </summary>
        string EmbeddedDatabaseStoragePath { get; set; }

        /// <summary>
        /// Gets or sets whether the Octopus server is allowed to check for upgrades.
        /// </summary>
        bool AllowCheckingForUpgrades { get; set; }

        /// <summary>
        /// Gets or sets whether anonymous usage statistics (# of projects, # of users, etc.) should be sent 
        /// when checking for upgrades.
        /// </summary>
        bool IncludeUsageStatisticsWhenCheckingForUpgrades { get; set; }

        /// <summary>
        /// Gets the directory that Octopus Server should use to store downloaded packages.
        /// </summary>
        string CacheDirectory { get; }

        /// <summary>
        /// Gets a directory where Octopus can store indexes for its internal NuGet repository.
        /// </summary>
        string PackagesIndexDirectory { get; }

        /// <summary>
        /// Gets the port number to use when connecting to the embedded RavenDB server.
        /// </summary>
        int RavenPort { get; }

        /// <summary>
        /// Gets the host name to use when connecting to the embedded RavenDB server.
        /// </summary>
        string RavenHostName { get; }

        /// <summary>
        /// Gets or sets the Raven database connection string.
        /// </summary>
        string RavenConnectionString { get; }

        /// <summary>
        /// Gets or sets whether the self host option is enabled.
        /// </summary>
        bool SelfHostWebPortal { get; set; }

        /// <summary>
        /// Gets or sets the port number that the self host server listens on.
        /// </summary>
        int SelfHostWebPortalPort { get; set; }

        /// <summary>
        /// Gets or sets whether domain authentication is used.
        /// </summary>
        AuthenticationMode AuthenticationMode { get; set; }
    }
}