using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public interface IPersistenceProvider
    {
        string GetValue(string key);
        void PersistValue(string key, string value);
    }

    public class PersistenceProvider : IPersistenceProvider
    {
        const string Name = "kubernetes-agent-metrics";
        readonly IKubernetesConfigMapService configMapService;
        readonly Lazy<V1ConfigMap> metricsConfigMap;
        readonly ISystemLog log;
        IDictionary<string, string> ConfigMapData => metricsConfigMap.Value.Data ??= new Dictionary<string, string>();

        public PersistenceProvider(IKubernetesConfigMapService configMapService, ISystemLog log)
        {
            this.configMapService = configMapService;
            this.log = log;
            metricsConfigMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public string GetValue(string key)
        {
            return ConfigMapData.TryGetValue(key, out var value) ? value : "";
        }

        public void PersistValue(string key, string value)
        {
            ConfigMapData[key] = value;
            configMapService.Patch(Name, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}