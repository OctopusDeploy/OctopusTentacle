using System.Collections.Generic;
using Halibut.Exceptions;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleCapabilitiesDecorator : ICapabilitiesService
    {
        private readonly ICapabilitiesService inner;

        public BackwardsCompatibleCapabilitiesDecorator(ICapabilitiesService inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponse GetCapabilities()
        {
            try
            {
                return inner.GetCapabilities();
            }
            catch (NoMatchingServiceOrMethodHalibutClientException)
            {
                return new CapabilitiesResponse(new List<string>());
            }
        }
    }
}