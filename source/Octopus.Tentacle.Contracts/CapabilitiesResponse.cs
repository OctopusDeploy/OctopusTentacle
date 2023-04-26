using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public class CapabilitiesResponse
    {
        public IReadOnlyList<string> SupportedCapabilities { get; }

        public CapabilitiesResponse(IReadOnlyList<string> supportedCapabilities)
        {
            this.SupportedCapabilities = supportedCapabilities;
        }
    }
}