using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public interface IKubernetesAgentMetrics
    {
        void TrackEvent(string reason, string source, DateTimeOffset occuredAt);

        DateTimeOffset GetLatestEventTimestamp();
    }
    
    public class KubernetesAgentMetrics : IKubernetesAgentMetrics
    {
        public delegate KubernetesAgentMetrics Factory(IPersistenceProvider persistenceProvider);
        
        readonly IPersistenceProvider persistenceProvider;
        readonly ISystemLog log;

        public KubernetesAgentMetrics(IPersistenceProvider persistenceProvider, ISystemLog log)
        {
            this.persistenceProvider = persistenceProvider;
            this.log = log;
        }

        public void TrackEvent(string reason, string source, DateTimeOffset occuredAt)
        {
            try
            {
                lock (persistenceProvider)
                {
                    var sourceEventsForReason = LoadFromPersistence(reason) ?? new Dictionary<string, List<DateTimeOffset>>();

                    if (!sourceEventsForReason.TryGetValue(source, out var occurenceTimestamps))
                    {
                        occurenceTimestamps = new List<DateTimeOffset>();
                        sourceEventsForReason[source] = occurenceTimestamps;
                    }
                    
                    occurenceTimestamps.Add(occuredAt);
                    Persist(reason, sourceEventsForReason);
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
                        JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>)
                    .SelectMany(dict => dict!.Values)
                    .SelectMany(ts => ts)
                    .OrderByDescending(ts => ts)
                    .FirstOrDefault();
            }
        }

        Dictionary<string, List<DateTimeOffset>>? LoadFromPersistence(string key)
        {
            var eventContent = persistenceProvider.GetValue(key);
            var configMapEvents = JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>(eventContent);

            return configMapEvents;
        }

        void Persist(string key, Dictionary<string, List<DateTimeOffset>> jsonEntry)
        {
            var jsonEncoded = JsonConvert.SerializeObject(jsonEntry);
            persistenceProvider.PersistValue(key, jsonEncoded);
        }
    }
}