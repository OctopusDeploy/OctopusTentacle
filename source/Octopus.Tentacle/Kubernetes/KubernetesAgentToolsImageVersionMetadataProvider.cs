using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Versioning;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IToolsImageVersionMetadataProvider
    {
        Task<KubernetesAgentToolsImageVersionMetadata?> TryGetVersionMetadata();
    }

    public class KubernetesAgentToolsImageVersionMetadataProvider : IToolsImageVersionMetadataProvider
    {
        readonly ISystemLog log;

        public KubernetesAgentToolsImageVersionMetadataProvider(ISystemLog log)
        {
            this.log = log;
        }
        
        public async Task<KubernetesAgentToolsImageVersionMetadata?> TryGetVersionMetadata()
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync("https://oc.to/kubernetes-agent-tools-image-metadata");
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<KubernetesAgentToolsImageVersionMetadata>(json);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to fetch version metadata for the agent tools container image. Details: {ex.Message}");
                return null;
            }
        }
    }
}