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
        /// Gets or sets the RavenDB connection string to use when  the <see cref="StorageMode"/> is <see cref="Configuration.StorageMode.External"/>.
        /// </summary>
        string ExternalDatabaseConnectionString { get; set; }
    }
}