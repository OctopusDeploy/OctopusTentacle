using System;

namespace Octopus.Shared.Configuration
{
    public interface IOctopusServerStorageConfiguration : IModifiableConfiguration, IMasterKeyConfiguration
    {
        /// <summary>
        /// Gets or sets a unique name for this server.
        /// </summary>
        string UniqueControllerName { get; set; }

        /// <summary>
        /// Gets or sets the SQL Server connection string to use.
        /// </summary>
        string ExternalDatabaseConnectionString { get; set; }
    }
}