namespace Octopus.Tentacle.Contracts.Capabilities
{
    public static class ICapabilitiesServiceV2ExtensionMethods
    {
        public static ICapabilitiesServiceV2 WithBackwardsCompatability(this ICapabilitiesServiceV2 capabilitiesService)
        {
            return new BackwardsCompatibleCapabilitiesV2Decorator(capabilitiesService);
        }
    }
}