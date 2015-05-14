using System;

namespace Octopus.Shared.Configuration
{
    public interface IOctopusServerStorageConfiguration : IModifiableConfiguration, IMasterKeyConfiguration
    {
        /// <summary>
        /// Gets or sets a unique node name for this server.
        /// </summary>
        string ServerNodeName { get; set; }

        /// <summary>
        /// Gets or sets the SQL Server connection string to use.
        /// </summary>
        string ExternalDatabaseConnectionString { get; set; }
    }
}