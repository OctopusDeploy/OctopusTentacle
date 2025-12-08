using System;

namespace Octopus.Tentacle
{
    public static class EnvironmentOverrides
    {
        public static bool UseLegacyExplicitSslConfiguration =>
            Environment.GetEnvironmentVariable("OCTOPUS_TENTACLE_USE_LEGACY_TLS") == "YES";
    }
}