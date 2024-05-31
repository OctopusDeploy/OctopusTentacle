using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public class KubernetesAgentMetrics
    {
        readonly MapFromConfigMapToEventList mapper;
        readonly IKubernetesConfigMapService configMapService;
        readonly ISystemLog log;
        const string Name = "kubernetes-agent-metrics";
        const string EntryName = "metrics";

        readonly Lazy<V1ConfigMap> metricsConfigMap;
        IDictionary<string, string> ConfigMapData => metricsConfigMap.Value.Data ??= new Dictionary<string, string>();

        public KubernetesAgentMetrics(IKubernetesConfigMapService configMapService, MapFromConfigMapToEventList mapper, ISystemLog log)
        {
            this.configMapService = configMapService;
            this.mapper = mapper;
            this.log = log;
            metricsConfigMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public void TrackEvent(EventRecord eventRecord)
        {
            try
            {
                lock (ConfigMapData)
                {
                    var existingEvents = LoadFromMap();
                    existingEvents.Add(eventRecord);
                    Persist(existingEvents);
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to persist a kubernetes event metric, {e.Message}");
            }
        }

        public DateTimeOffset? GetLatestEventTimestamp()
        {
            try
            {
                lock (ConfigMapData)
                {
                    var existingEvents = LoadFromMap();
                    return existingEvents
                        .OrderByDescending(re => re.Timestamp)
                        .Select(re => re.Timestamp)
                        .FirstOrDefault();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        List<EventRecord> LoadFromMap()
        {
            var eventContent = GetDataFromMap(EntryName);
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

        string GetDataFromMap(string entryName)
        {
            if (ConfigMapData.TryGetValue(entryName, out var value))
            {
                return value;
            }

            return "";
        }
    }
}