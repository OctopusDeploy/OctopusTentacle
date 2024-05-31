using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public class KubernetesAgentMetrics
    {
        readonly MapFromConfigMapToEventList mapper;
        readonly IKubernetesConfigMapService configMapService;
        const string Name = "kubernetes-agent-metrics";
        const string EntryName = "metrics";

        readonly Lazy<V1ConfigMap> metricsConfigMap;
        IDictionary<string, string> ConfigMapData => metricsConfigMap.Value.Data ??= new Dictionary<string, string>();

        public KubernetesAgentMetrics(IKubernetesConfigMapService configMapService, MapFromConfigMapToEventList mapper)
        {
            this.configMapService = configMapService;
            this.mapper = mapper;
            metricsConfigMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public void TrackEvent(EventRecord eventRecord)
        {
            try
            {
                lock (ConfigMapData)
                {
                    var existingEvents = GetEventRecords();
                    existingEvents.Add(eventRecord);
                    Persist(existingEvents);
                }
            }
            catch (Exception)
            {
                //no idea how to actually log this exception, nor to where
                //will land here if the metrics-config-map does not exist.
            }
        }

        List<EventRecord> GetEventRecords()
        {
            var eventContent = GetDataFromMap();
            var configMapEvents = JsonConvert.DeserializeObject<List<EventRecord>>(eventContent);

            if (configMapEvents is null)
            {
                return new List<EventRecord>();
            }
            
            return mapper.FromConfigMap(configMapEvents);
        }

        void Persist(List<EventRecord> eventRecords)
        {
            var configMapData = mapper.ToConfigMap(eventRecords);
            ConfigMapData[EntryName] = JsonConvert.SerializeObject(configMapData);
            configMapService.Patch(Name, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
        }

        string GetDataFromMap()
        {
            if (ConfigMapData.TryGetValue(EntryName, out var value))
            {
                return value;
            }

            return "";
        }
    }
}