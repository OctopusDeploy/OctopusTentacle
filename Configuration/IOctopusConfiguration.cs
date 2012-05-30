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
    }
}