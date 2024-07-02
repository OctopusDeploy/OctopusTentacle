using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Properties;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Capabilities
{
    [Service(typeof(ICapabilitiesServiceV2))]
    public class CapabilitiesServiceV2 : IAsyncCapabilitiesServiceV2
    {
        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var versionInformation = $"Tentacle_Version_{OctopusTentacle.InformationalVersion}";

            //the kubernetes agent only supports the kubernetes script services
            if (PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                return new CapabilitiesResponseV2(new List<string> { nameof(IFileTransferService), nameof(IKubernetesScriptServiceV1Alpha), nameof(IKubernetesScriptServiceV1), versionInformation });
            }

            //non-kubernetes agent tentacles only support the standard script services
            return new CapabilitiesResponseV2(new List<string> { nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2), versionInformation });
        }
    }
}