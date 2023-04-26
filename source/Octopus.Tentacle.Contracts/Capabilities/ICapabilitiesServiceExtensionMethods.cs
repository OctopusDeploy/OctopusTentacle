namespace Octopus.Tentacle.Contracts.Capabilities
{
    public static class ICapabilitiesServiceExtensionMethods
    {
        public static ICapabilitiesService WithBackwardsCompatability(this ICapabilitiesService capabilitiesService)
        {
            return new BackwardsCompatibleCapabilitiesDecorator(capabilitiesService);
        }
    }
}