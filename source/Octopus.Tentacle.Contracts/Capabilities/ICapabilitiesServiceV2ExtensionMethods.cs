using Octopus.Tentacle.Client.ClientServices;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public static class ICapabilitiesServiceV2ExtensionMethods
    {
        public static ICapabilitiesServiceV2 WithBackwardsCompatability(this ICapabilitiesServiceV2 capabilitiesService)
        {
            return new BackwardsCompatibleCapabilitiesV2Decorator(capabilitiesService);
        }
        
        public static IClientCapabilitiesServiceV2 WithBackwardsCompatability(this IClientCapabilitiesServiceV2 capabilitiesService)
        {
            return new BackwardsCompatibleClientCapabilitiesV2Decorator(capabilitiesService);
        }
    }
}