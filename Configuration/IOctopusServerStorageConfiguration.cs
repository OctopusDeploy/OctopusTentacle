using System;

namespace Octopus.Shared.Configuration
{
    public interface IOctopusServerStorageConfiguration
    {
        /// <summary>
        /// Gets or sets the RavenDB storage mode.
        /// </summary>
        StorageMode StorageMode { get; set; }

        /// <summary>
        /// Gets or sets where Octopus should store the RavenDB data on the Octopus server  when the <see cref="StorageMode"/> is <see cref="Configuration.StorageMode.Embedded"/>.
        /// </summary>
        string EmbeddedDatabaseStoragePath { get; }

        /// <summary>
        /// Gets or sets the port that the embedded database should listen on for external connections when the <see cref="StorageMode"/> is <see cref="Configuration.StorageMode.Embedded"/>. Values 0 or below mean that the embedded database won't be available externally. 
        /// </summary>
        int EmbeddedDatabaseListenPort { get; set; }

        /// <summary>
        /// Gets or sets the address that the embedded database will listen on.
        /// </summary>
        string EmbeddedDatabaseListenHostname { get; set; }

        /// <summary>
        /// Gets or sets the RavenDB connection string to use when  the <see cref="StorageMode"/> is <see cref="Configuration.StorageMode.External"/>.
        /// </summary>
        string ExternalDatabaseConnectionString { get; set; }

        /// <summary>
        /// Gets or sets whether Octopus should set up a default backup policy.
        /// </summary>
        bool BackupsEnabledByDefault { get; set; }
    }
}