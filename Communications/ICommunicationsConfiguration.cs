using System;

namespace Octopus.Shared.Communications
{
    /// <summary>
    /// Configures how the local instance communicates with other instances.
    /// </summary>
    public interface ICommunicationsConfiguration
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
    }
}
