using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public class CapabilitiesResponse
    {
        public IReadOnlyList<string> capabilities { get; }

        public CapabilitiesResponse(IReadOnlyList<string> capabilities)
        {
            this.capabilities = capabilities;
        }
    }
}