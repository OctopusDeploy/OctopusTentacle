using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public interface IPersistenceProvider
    {
        Task<string?> GetValue(string key, CancellationToken cancellationToken);
        Task PersistValue(string key, string value, CancellationToken cancellationToken);
        Task<ImmutableDictionary<string, string>> ReadValues(CancellationToken cancellationToken);
    }

    public class PersistenceProvider : IPersistenceProvider
    {
        public delegate PersistenceProvider Factory(string configMapName);
        
        readonly string configMapName;
        readonly IKubernetesConfigMapService configMapService;

        public PersistenceProvider(string configMapName, IKubernetesConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            this.configMapName = configMapName;
        }

        public async Task<string?> GetValue(string key, CancellationToken cancellationToken)
        {
            var configMap = await configMapService.TryGet(configMapName, cancellationToken);
            return configMap?.Data.TryGetValue(key, out var value) == true ? value : null;
        }

        public async Task PersistValue(string key, string value, CancellationToken cancellationToken)
        {
            var configMap = await configMapService.TryGet(configMapName, cancellationToken);
            if (configMap is null) throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}");

            configMap.Data[key] = value;
            await configMapService.Patch(configMapName, configMap.Data, cancellationToken);
        }

        public async Task<ImmutableDictionary<string, string>> ReadValues(CancellationToken cancellationToken)
        {
            var configMap = await configMapService.TryGet(configMapName, cancellationToken);

            return configMap?.Data.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
        }
    }
}