using System;

namespace Octopus.Tentacle
{
    public static class EnvironmentOverrides
    {
        /// <summary>
        /// By default, (i.e. with no environment variable set) we will use the legacy explicit SSL
        /// configuration. For users that choose to opt-in to the new behavior early they can set
        /// the OCTOPUS_TENTACLE_USE_LEGACY_TLS environment variable to "FALSE".
        ///
        /// In the future, the default will change to using the system default, and this flag will
        /// exist to allow opting back into the legacy behavior by setting the
        /// OCTOPUS_TENTACLE_USE_LEGACY_TLS environment variable to "TRUE".
        /// </summary>
        public static bool UseLegacyExplicitSslConfiguration =>
            !bool.FalseString.Equals(
                Environment.GetEnvironmentVariable("OCTOPUS_TENTACLE_USE_LEGACY_TLS"),
                StringComparison.OrdinalIgnoreCase
            );
    }
}