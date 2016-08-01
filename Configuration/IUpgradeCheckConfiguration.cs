using System;
using Octopus.Server.Extensibility.Configuration;

namespace Octopus.Shared.Configuration
{
    public interface IUpgradeCheckConfiguration : IModifiableConfiguration
    {
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
        /// Gets or sets whether unhandled errors can be reported online.
        /// </summary>
        bool ReportErrorsOnline { get; set; }
    }
}