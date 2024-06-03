using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

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

        public void TrackEvent(string reason, string source, DateTimeOffset occurrence)
        {
            try
            {
                lock (persistenceProvider)
                {
                    var existingEvent = LoadFromPersistence(reason) ?? new EventJsonEntry(source, new List<DateTimeOffset>());
                    existingEvent.Occurrences.Add(occurrence);
                    Persist(reason, existingEvent);
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
                var allEvents = persistenceProvider.ReadValues();

                return allEvents.Values.Select(
                        JsonConvert.DeserializeObject<EventJsonEntry>)
                    .WhereNotNull()
                    .SelectMany(eje => eje.Occurrences)
                    .OrderByDescending(timestamp => timestamp)
                    .FirstOrDefault();
            }
        }

        EventJsonEntry? LoadFromPersistence(string key)
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

    internal record EventJsonEntry(string Source, List<DateTimeOffset> Occurrences);
}