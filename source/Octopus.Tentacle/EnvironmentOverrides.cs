using System;

namespace Octopus.Tentacle
{
    public static class EnvironmentOverrides
    {
        /// <summary>
        /// By default, (i.e. with no environment variable set) we will use the system default SSL
        /// configuration. For users that need to revert to the legacy explicit SSL configuration,
        /// they can set the OCTOPUS_TENTACLE_USE_LEGACY_TLS environment variable to "TRUE".
        /// </summary>
        public static bool UseLegacyExplicitSslConfiguration =>
            bool.TrueString.Equals(
                Environment.GetEnvironmentVariable("OCTOPUS_TENTACLE_USE_LEGACY_TLS"),
                StringComparison.OrdinalIgnoreCase
            );
    }
}