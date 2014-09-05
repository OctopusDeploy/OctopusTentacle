using System;

namespace Octopus.Shared.Conventions
{
    public static class HostedSiphonConventions
    {
        static string postfix = "Siphon";

        public static string GetHostedApplicationName(string octopusApplicationName)
        {
            return octopusApplicationName + postfix;
        }

        public static string GetServerApplicationNameFromHosted(string siphonApplicationName)
        {
            var index = siphonApplicationName.IndexOf(postfix, System.StringComparison.Ordinal);
            return siphonApplicationName.Substring(0, index);
        }
    }
}
