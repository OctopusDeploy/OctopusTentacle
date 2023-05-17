using System.Collections.Generic;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service]
    public class CapabilitiesServiceV2 : ICapabilitiesServiceV2
    {
        public CapabilitiesResponseV2 GetCapabilities()
        {
            return new CapabilitiesResponseV2(new List<string>() {nameof(IScriptService), nameof(IFileTransferService)});
        }
    }
}