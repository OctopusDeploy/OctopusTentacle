using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public class KubernetesAgentMetrics
    {
        readonly string lastEventTimestampKey = "latestTimestamp";

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
            DateTimeOffset latestEvent;
            try
            {
                latestEvent = GetLatestEventTimestamp();
            }
            catch
            {
                return;
            }
            
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

                    occurenceTimestamps.Add(occurrence);

                    // The config map should _probably_ be written up as an update/commit process which makes the
                    // persistence atomic
                    Persist(reason, sourceEventsForReason);
                    if (latestEvent < occurrence)
                    {
                        persistenceProvider.PersistValue(lastEventTimestampKey, occurrence.ToString("O"));
                    }
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
                var timeStampString = persistenceProvider.GetValue(lastEventTimestampKey);
                if (!timeStampString.IsNullOrEmpty())
                {
                    return DateTimeOffset.Parse(timeStampString!);    
                }
                return DateTimeOffset.MinValue;
            }
        }

        Dictionary<string, List<DateTimeOffset>>? LoadFromPersistence(string key)
        {
            var eventContent = persistenceProvider.GetValue(key) ?? "";
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