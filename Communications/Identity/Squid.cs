using System;

namespace Octopus.Shared.Communications.Identity
{
    public static class Squid
    {
        public static string NewSquid(string humanComponent = null)
        {
            var newSquid = "SQ-" +
                           (humanComponent ?? Environment.MachineName) + "-" +
                           Guid.NewGuid().GetHashCode().ToString("X8");
            return NormalizeSquid(newSquid);
        }

        static string NormalizeSquid(string squid)
        {
            return squid.ToUpperInvariant();
        }
    }
}
