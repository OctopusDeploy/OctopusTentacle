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

        public static bool HasAbandonScriptV2(this CapabilitiesResponseV2 capabilities)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            // Tentacle advertises this as nameof(ScriptServiceV2.AbandonScriptAsync). Keep the
            // literal in sync with CapabilitiesServiceV2.GetCapabilitiesAsync on the Tentacle side.
            return capabilities.SupportedCapabilities.Contains("AbandonScriptAsync");
        }
    }
}