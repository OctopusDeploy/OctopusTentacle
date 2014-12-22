using System;

namespace Octopus.Platform.Deployment.Configuration
{
    /// <summary>
    /// Configures how the local instance communicates with other instances.
    /// </summary>
    public interface ICommunicationsConfiguration : IModifiableConfiguration
    {
        /// <summary>
        /// Gets or sets the SQUID uniquely identifying the local instance. This value must be unique
        /// among all entities in an Octopus network.
        /// </summary>
        string Squid { get; set; }

        /// <summary>
        /// Gets the directory in which queued messages are stored.
        /// </summary>
        string MessagesDirectory { get; }

        /// <summary>
        /// Gets the directory in which the state of running activities is stored.
        /// </summary>
        string ActorStateDirectory { get; }

        /// <summary>
        /// Gets the directory in which file streams are stored.
        /// </summary>
        string StreamsDirectory { get; }

        /// <summary>
        /// The port on which incoming communications will be received.
        /// </summary>
        int ServicesPort { get; set; }

        /// <summary>
        /// The chunk size, in bytes, to use when transferring files
        /// between machines.
        /// </summary>
        long FileTransferChunkSizeBytes { get; set; }
    }
}
