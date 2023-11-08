using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service(typeof(ICapabilitiesServiceV2))]
    public class CapabilitiesServiceV2 : IAsyncCapabilitiesServiceV2
    {
        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new CapabilitiesResponseV2(new List<string>() {nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2)});
        }
    }
}