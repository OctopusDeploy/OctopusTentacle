using System.Linq;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Capabilities
{
    internal static class CapabilitiesResponseV2ExtensionMethods
    {
        public static bool HasScriptServiceV2(this CapabilitiesResponseV2 capabilities, TentacleClientOptions clientOptions)
        {
            if (capabilities?.SupportedCapabilities?.Any() != true)
            {
                return false;
            }

            const string serviceName = nameof(IScriptServiceV2);
            //if the service is not explicitly disabled and is supported by the tentacle
            return !clientOptions.DisabledScriptServices.Contains(serviceName) &&
                capabilities.SupportedCapabilities.Contains(serviceName);
        }
    }
}