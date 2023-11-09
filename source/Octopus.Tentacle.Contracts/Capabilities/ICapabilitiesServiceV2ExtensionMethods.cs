using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public static class ICapabilitiesServiceV2ExtensionMethods
    {
        public static ICapabilitiesServiceV2 WithBackwardsCompatability(this ICapabilitiesServiceV2 capabilitiesService)
        {
            return new BackwardsCompatibleCapabilitiesV2Decorator(capabilitiesService);
        }

        public static IAsyncClientCapabilitiesServiceV2 WithBackwardsCompatability(this IAsyncClientCapabilitiesServiceV2 capabilitiesService)
        {
            return new BackwardsCompatibleAsyncClientCapabilitiesV2Decorator(capabilitiesService);
        }
    }
}