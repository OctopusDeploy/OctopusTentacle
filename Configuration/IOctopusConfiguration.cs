using System;

namespace Octopus.Shared.Configuration
{
    /// <summary>
    /// Machine-wide Octopus configuration settings (backed by the Windows Registry). 
    /// </summary>
    public interface IOctopusConfiguration
    {
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
        /// Gets a directory where Octopus can store packages for its internal NuGet repository.
        /// </summary>
        string PackagesDirectory { get; set; }

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
        /// Gets the web portal address to use to contact the internal NuGet feed.
        /// </summary>
        string PublicWebPortalAddress { get; set; }

        /// <summary>
        /// Gets the web portal address to use to contact the internal NuGet feed from the Octopus server.
        /// </summary>
        string LocalWebPortalAddress { get; set; }

        /// <summary>
        /// Gets where the local address should be automatically configured.
        /// </summary>
        bool LocalWebPortalAddressAutoConfigure { get; set; }

        /// <summary>
        /// Special API key that can be used by the Octopus server when querying the integrated package repository.
        /// </summary>
        string IntegratedFeedApiKey { get; }

        void EnsureIntegratedFeedApiKey();
    }
}