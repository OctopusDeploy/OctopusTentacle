using System;

namespace Octopus.Platform.Deployment.Configuration
{
    public interface IMasterKeyConfiguration
    {
        /// <summary>
        /// The encryption key used for bulk data.
        /// </summary>
        byte[] MasterKey { get; set; }
    }
}