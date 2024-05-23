using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using k8s.Models;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
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
        const string Name = "tentacle-metrics";

        readonly Lazy<V1ConfigMap> configMap;
        IDictionary<string, string> ConfigMapData => configMap.Value.Data ??= new Dictionary<string, string>();

        public KubernetesAgentMetrics(IKubernetesConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            configMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public void IncrementMetric(KubernetesMetric metric)
        {
            var metricName = Enum.GetName(typeof(KubernetesMetric), metric)!;
            lock (ConfigMapData)
            {
                var initialValue = GetValueFromMap(metricName);
                ConfigMapData[metricName] = (initialValue + 1).ToString();
                Persist();
            }
        }

        // Chances are, this needs to happen in the healthcheck script
        // public void Reset()
        // {
        //     var values = Enum.GetValues(typeof(KubernetesMetric)).Cast<KubernetesMetric>();
        //     lock (ConfigMapData)
        //     {
        //         foreach (var metric in values)
        //         {
        //             var metricName = Enum.GetName(typeof(KubernetesMetric), metric)!;
        //             ConfigMapData[metricName] = "0";
        //             ConfigMapData[metricName] = "0";
        //             Persist();
        //         }
        //     }
        // }

        // Chances are, this needs to happen in the healthcheck script
        // public ImmutableDictionary<string, int> Extract()
        // {
        //     var metrics = Enum.GetValues(typeof(KubernetesMetric)).Cast<KubernetesMetric>();
        //     var result = new Dictionary<string, int>();
        //     lock (ConfigMapData)
        //     {
        //         foreach (var metric in metrics)
        //         {
        //             var metricName = Enum.GetName(typeof(KubernetesMetric), metric)!;
        //             result[metricName] = GetValueFromMap(metricName);
        //         }
        //     }
        //
        //     return result.ToImmutableDictionary();
        // }

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