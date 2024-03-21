using System.Linq;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

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

        public static bool HasAnyKubernetesScriptService(this CapabilitiesResponseV2 capabilities)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            return capabilities.SupportedCapabilities.Any(c => c.Contains("KubernetesScriptService"));
        }
    }
}