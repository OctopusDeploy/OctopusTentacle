using System;

namespace Octopus.Shared.Conventions
{
    public static class HostedRelayConventions
    {
        static string postfix = " Relay";

        public static string GetHostedApplicationName(string octopusApplicationName)
        {
            return octopusApplicationName + postfix;
        }

        public static string GetServerApplicationNameFromHosted(string relayApplicationName)
        {
            var index = relayApplicationName.IndexOf(postfix, System.StringComparison.Ordinal);
            return relayApplicationName.Substring(0, index);
        }
    }
}
