using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public interface IPersistenceProvider
    {
        string GetValue(string key);
        void PersistValue(string key, string value);
    }

    public class PersistenceProvider : IPersistenceProvider
    {
        readonly string configMapName;
        readonly IKubernetesConfigMapService configMapService;
        readonly Lazy<V1ConfigMap> metricsConfigMap;
        IDictionary<string, string> ConfigMapData => metricsConfigMap.Value.Data ??= new Dictionary<string, string>();

        public PersistenceProvider(string configMapName, IKubernetesConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            this.configMapName = configMapName;
            metricsConfigMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(this.configMapName, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public string GetValue(string key)
        {
            return ConfigMapData.TryGetValue(key, out var value) ? value : "";
        }

        public void PersistValue(string key, string value)
        {
            ConfigMapData[key] = value;
            configMapService.Patch(configMapName, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}