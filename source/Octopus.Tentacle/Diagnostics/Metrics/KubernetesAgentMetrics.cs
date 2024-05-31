using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Diagnostics.Metrics
{
    public class KubernetesAgentMetrics
    {
        readonly IPersistenceProvider persistenceProvider;
        readonly MapFromConfigMapToEventList mapper;
        readonly ISystemLog log;
        public const string EntryName = "metrics";

        public KubernetesAgentMetrics(IPersistenceProvider persistenceProvider, MapFromConfigMapToEventList mapper, ISystemLog log)
        {
            this.persistenceProvider = persistenceProvider;
            this.mapper = mapper;
            this.log = log;
        }

        public void TrackEvent(EventRecord eventRecord)
        {
            try
            {
                lock (persistenceProvider)
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

        public DateTimeOffset GetLatestEventTimestamp()
        {
            lock (persistenceProvider)
            {
                var existingEvents = LoadFromMap();
                return existingEvents
                    .Select(re => re.Timestamp)
                    .OrderByDescending(ts => ts)
                    .FirstOrDefault();
            }
        }

        List<EventRecord> LoadFromMap()
        {
            var eventContent = persistenceProvider.GetValue(EntryName);
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
            var jsonEncoded = JsonConvert.SerializeObject(configMapData);
            persistenceProvider.PersistValue(EntryName, jsonEncoded);
        }
    }
}