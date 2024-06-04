using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public interface IKubernetesAgentMetrics
    {
        void TrackEvent(string reason, string source, DateTimeOffset firstOccurrence, int count);
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

        public void TrackEvent(string reason, string source, DateTimeOffset firstOccurrence, int countSince)
        {
            try
            {
                lock (persistenceProvider)
                {
                    var sourceEventsForReason = LoadFromPersistence(reason) ?? new SourceEventCounts();

                    sourceEventsForReason.SetEventCount(source, firstOccurrence, countSince);

                    Persist(reason, sourceEventsForReason);
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to persist a kubernetes event metric, {e.Message}");
            }
        }

        SourceEventCounts? LoadFromPersistence(string key)
        {
            var eventContent = persistenceProvider.GetValue(key);
            var configMapEvents = JsonConvert.DeserializeObject<SourceEventCounts>(eventContent);

            return configMapEvents;
        }

        void Persist(string key, Dictionary<string, List<CountSinceEpoch>> jsonEntry)
        {
            var jsonEncoded = JsonConvert.SerializeObject(jsonEntry);
            persistenceProvider.PersistValue(key, jsonEncoded);
        }
    }

    public class CountSinceEpoch
    {
        public DateTimeOffset Epoch { get; }
        public int Count { get; set; }

        public CountSinceEpoch(DateTimeOffset epoch, int count)
        {
            Epoch = epoch;
            Count = count;
        }
    }

    public class SourceEventCounts : Dictionary<string, List<CountSinceEpoch>>
    {
        public void SetEventCount(string source, DateTimeOffset firstOccurrence, int count)
        {
            if (!TryGetValue(source, out var eventCount))
            {
                eventCount = new List<CountSinceEpoch>();
                Add(source, eventCount);
            }
            
            var existingEntry = eventCount.FirstOrDefault(e => e.Epoch == firstOccurrence);
            if (existingEntry is null)
            {
                eventCount.Add(new CountSinceEpoch(firstOccurrence, count));
            }
            else
            {
                existingEntry.Count = count;
            }
        }
    }
}