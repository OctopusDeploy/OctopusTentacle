using System.Linq;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Capabilities
{
    internal static class CapabilitiesResponseV2ExtensionMethods
    {
        public static bool HasScriptServiceV2(this CapabilitiesResponseV2 capabilities)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            return capabilities.SupportedCapabilities.Contains(nameof(IScriptServiceV2));
        }

        public static bool HasAbandonScript(this CapabilitiesResponseV2 capabilities)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            // Both sides nameof IScriptServiceV2.AbandonScript, so the strings match and a rename
            // on either side can't silently drift the capability check.
            return capabilities.SupportedCapabilities.Contains(nameof(IScriptServiceV2.AbandonScript));
        }
    }
}