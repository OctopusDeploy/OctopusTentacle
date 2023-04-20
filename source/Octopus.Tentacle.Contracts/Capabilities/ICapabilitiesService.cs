using System;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public interface ICapabilitiesService
    {
        public CapabilitiesResponse SupportedCapabilities();
    }
}