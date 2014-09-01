using System;

namespace Octopus.Shared.Conventions
{
    public static class HostedSiphonConventions
    {
        public static string GetHostedApplicationName(string octopusApplicationName)
        {
            return octopusApplicationName + "Siphon";
        }

        public static string GetServerApplicationNameFromHosted(string siphonApplicationName)
        {
            var index = siphonApplicationName.IndexOf("Siphon", System.StringComparison.Ordinal);
            return siphonApplicationName.Substring(0, index);
        }
    }
}
