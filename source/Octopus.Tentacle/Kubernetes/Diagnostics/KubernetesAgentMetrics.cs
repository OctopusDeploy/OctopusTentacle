using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public class KubernetesAgentMetrics
    {
        public delegate KubernetesAgentMetrics Factory(IPersistenceProvider persistenceProvider);
        
        readonly IPersistenceProvider persistenceProvider;
        readonly ISystemLog log;

        public KubernetesAgentMetrics(IPersistenceProvider persistenceProvider, ISystemLog log)
        {
            this.persistenceProvider = persistenceProvider;
            this.log = log;
        }

        public void TrackEvent(EventRecord eventRecord)
        {
            try
            {
                lock (persistenceProvider)
                {
                    var existingEvent = LoadFromMap(eventRecord.Reason);
                    if (existingEvent is null)
                    {
                        existingEvent = new EventJsonEntry(eventRecord.Source, new List<DateTimeOffset>());
                    }
                    existingEvent.Occurrences.Add(eventRecord.Timestamp);
                    Persist(eventRecord.Reason, existingEvent);
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

        EventJsonEntry? LoadFromMap(string key)
        {
            var eventContent = persistenceProvider.GetValue(key);
            var configMapEvents = JsonConvert.DeserializeObject<EventJsonEntry>(eventContent);

            return configMapEvents;

        }

        void Persist(string key, EventJsonEntry jsonEntry)
        {
            var jsonEncoded = JsonConvert.SerializeObject(jsonEntry);
            persistenceProvider.PersistValue(key, jsonEncoded);
        }
    }

    public record EventJsonEntry(string Source, List<DateTimeOffset> Occurrences);
}