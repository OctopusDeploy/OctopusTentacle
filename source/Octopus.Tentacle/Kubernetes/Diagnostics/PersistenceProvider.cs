using System;
using System.Collections.Generic;
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
        readonly IKubernetesConfiguration kubernetesConfiguration;
        readonly IKubernetesConfigMapService configMapService;

        public PersistenceProvider(string configMapName, IKubernetesConfiguration kubernetesConfiguration, IKubernetesConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            this.configMapName = configMapName;
            this.kubernetesConfiguration = kubernetesConfiguration;
        }

        public async Task<string?> GetValue(string key, CancellationToken cancellationToken)
        {
            var configMapData = await LoadConfigMapData(cancellationToken);
            return configMapData.TryGetValue(key, out var value) ? value : null;
        }

        public async Task PersistValue(string key, string value, CancellationToken cancellationToken)
        {
            var configMapData = await LoadConfigMapData(cancellationToken);
            if (configMapData is null) throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {kubernetesConfiguration.Namespace}");

            configMapData[key] = value;
            await configMapService.Patch(configMapName, configMapData, cancellationToken);
        }

        public async Task<ImmutableDictionary<string, string>> ReadValues(CancellationToken cancellationToken)
        {
            var configMapData = await LoadConfigMapData(cancellationToken);
            return configMapData.ToImmutableDictionary();
        }

        async Task<IDictionary<string, string>> LoadConfigMapData(CancellationToken cancellationToken)
        {
            var configMap = await configMapService.TryGet(configMapName, cancellationToken);
            if (configMap is null) throw new InvalidOperationException($"ConfigMap {configMapName} not found");
            return configMap.Data ?? new Dictionary<string, string>();
        }
    }
}