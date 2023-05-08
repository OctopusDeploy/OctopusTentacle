using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

/* Unmerged change from project 'Octopus.Tentacle (net6.0)'
Before:
using Octopus.Tentacle.Contracts.Capabilities;
After:
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.IScriptServiceV2;
using Octopus.Tentacle.Contracts.IScriptServiceV2.IScriptServiceV2;
*/
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service]
    public class CapabilitiesService : ICapabilitiesService
    {
        public CapabilitiesResponse GetCapabilities()
        {
            return new CapabilitiesResponse(new []{ nameof(IScriptServiceV2)+"Alpha" });
        }
    }
}