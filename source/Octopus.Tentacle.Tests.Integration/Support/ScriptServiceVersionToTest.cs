using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum ScriptServiceVersionToTest
    {
        /// <summary>
        /// Indicates that per-script service testing should not occur
        /// </summary>
        None,
        /// <summary>
        /// Indicates that per-script service testing should be based on the service versions supported by the target tentacle version
        /// </summary>
        TentacleSupported,
        /// <summary>
        /// Test ScriptService V1 only
        /// </summary>
        Version1,
        /// <summary>
        /// Test ScriptService V2 only
        /// </summary>
        Version2,
        /// <summary>
        /// Test ScriptService V3Alpha only
        /// </summary>
        Version3Alpha
    }
}
