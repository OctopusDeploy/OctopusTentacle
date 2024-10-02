using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IToolsImageVersionMetadataProvider
    {
        Task<KubernetesAgentToolsImageVersionMetadata?> TryGetVersionMetadata();
    }

    public class CachingKubernetesAgentToolsImageVersionMetadataProvider : IToolsImageVersionMetadataProvider
    {
        readonly IToolsImageVersionMetadataProvider inner;
        readonly IMemoryCache memoryCache;
        static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);
        
        public CachingKubernetesAgentToolsImageVersionMetadataProvider(IToolsImageVersionMetadataProvider inner, IMemoryCache memoryCache)
        {
            this.inner = inner;
            this.memoryCache = memoryCache;
        }
        
        public Task<KubernetesAgentToolsImageVersionMetadata?> TryGetVersionMetadata()
        {
            var cacheKey = $"{nameof(CachingKubernetesAgentToolsImageVersionMetadataProvider)}_VersionMetadata";
            return memoryCache.GetOrCreateAsync<KubernetesAgentToolsImageVersionMetadata?>(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheExpiry;
                return await inner.TryGetVersionMetadata();
            });
        }
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