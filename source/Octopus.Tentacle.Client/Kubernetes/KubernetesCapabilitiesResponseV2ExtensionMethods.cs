using System.Linq;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;

namespace Octopus.Tentacle.Client.Kubernetes
{
    public static class KubernetesCapabilitiesResponseV2ExtensionMethods
    {
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