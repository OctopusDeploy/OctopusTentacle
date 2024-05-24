using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Diagnostics
{
    public enum KubernetesMetric
    {
        NFS_SCRIPT_KILL_POD_COUNT,
        SOMETHING_ELSE
    }

    public class KubernetesAgentMetrics
    {
        readonly IKubernetesConfigMapService configMapService;
        const string Name = "kubernetes-agent-metrics";

        readonly Lazy<V1ConfigMap> metricsConfigMap;
        IDictionary<string, string> ConfigMapData => metricsConfigMap.Value.Data ??= new Dictionary<string, string>();

        public KubernetesAgentMetrics(IKubernetesConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            metricsConfigMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public void IncrementMetric(KubernetesMetric metric)
        {
            var metricName = Enum.GetName(typeof(KubernetesMetric), metric)!;
            try
            {
                lock (ConfigMapData)
                {
                    var initialValue = GetValueFromMap(metricName);
                    ConfigMapData[metricName] = (initialValue + 1).ToString();
                    Persist();
                }
            }
            catch (Exception)
            {
                //no idea how to actually log this exception, nor to where
                //will land here if the metrics-config-map does not exist.
            }
        }

        void Persist()
        {
            configMapService.Patch(Name, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
        }

        int GetValueFromMap(string metricName)
        {
            int initialValue = 0;
            if (ConfigMapData.TryGetValue(metricName, out var value))
            {
                int.TryParse(value, out initialValue);
            }

            return initialValue;
        }
    }
}