using System.Collections.Generic;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service]
    public class CapabilitiesService : ICapabilitiesService
    {
        public CapabilitiesResponse SupportedCapabilities()
        {
            return new CapabilitiesResponse(new List<string>());
        }
    }
}