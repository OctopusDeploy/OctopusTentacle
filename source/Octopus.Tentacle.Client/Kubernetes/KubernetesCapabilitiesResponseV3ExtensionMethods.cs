using System.Linq;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Client.Kubernetes
{
    public static class KubernetesCapabilitiesResponseV3ExtensionMethods
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