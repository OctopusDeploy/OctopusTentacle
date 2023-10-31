using System.Collections.Generic;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service(typeof(ICapabilitiesServiceV2))]
    public class CapabilitiesServiceV2 : ICapabilitiesServiceV2
    {
        public CapabilitiesResponseV2 GetCapabilities()
        {
            return new CapabilitiesResponseV2(new List<string>() {nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2), nameof(IScriptServiceV3Alpha)});
        }
    }
}