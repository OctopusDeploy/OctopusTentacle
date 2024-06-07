using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    public class KubernetesAgentMetrics
    {
        readonly string lastEventTimestampKey = "latestTimestamp";

        public delegate KubernetesAgentMetrics Factory(IPersistenceProvider persistenceProvider);

        readonly SemaphoreSlim semaphore = new(1);
        readonly IPersistenceProvider persistenceProvider;
        readonly ISystemLog log;

        public KubernetesAgentMetrics(IPersistenceProvider persistenceProvider, ISystemLog log)
        {
            this.persistenceProvider = persistenceProvider;
            this.log = log;
        }

        public async Task TrackEvent(string reason, string source, DateTimeOffset occurrence, CancellationToken cancellationToken)
        {
            try
            {
                using var _ = await semaphore.LockAsync(cancellationToken);
                var sourceEvents = await LoadFromPersistence(cancellationToken) ?? new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>();
                if (!sourceEvents.TryGetValue(reason, out var sourceEventsForReason))
                {
                    sourceEventsForReason = new Dictionary<string, List<DateTimeOffset>>();
                    sourceEvents[reason] = sourceEventsForReason;
                }
                if (!sourceEventsForReason.TryGetValue(source, out var occurenceTimestamps))
                {
                    occurenceTimestamps = new List<DateTimeOffset>();
                    sourceEventsForReason[source] = occurenceTimestamps;
                }

                occurenceTimestamps.Add(occurrence);
                await Persist(reason, sourceEventsForReason, cancellationToken);
                await UpdateLatestTimestamp(occurrence, cancellationToken);
            }
            catch (Exception e)
            {
                log.Error($"Failed to persist a kubernetes event metric, {e.Message}");
            }
        }

        public async Task<DateTimeOffset> GetLatestEventTimestamp(CancellationToken cancellationToken)
        {
            using var _ = await semaphore.LockAsync(cancellationToken);
            return await GetLatestEventTimeStampInternal(cancellationToken);
        }

        async Task<DateTimeOffset> GetLatestEventTimeStampInternal(CancellationToken cancellationToken)
        {
            //NOTE: this must be called from within a lock on the PersistenceProvider.
            var timeStampString = await persistenceProvider.GetValue(lastEventTimestampKey, cancellationToken);
            if (!timeStampString.IsNullOrEmpty())
            {
                return DateTimeOffset.Parse(timeStampString!);    
            }
            return DateTimeOffset.MinValue;
        }

        async Task UpdateLatestTimestamp(DateTimeOffset newEventTime, CancellationToken cancellationToken)
        {
            try
            {
                var latestEvent = await GetLatestEventTimestamp(cancellationToken);
                // The config map should _probably_ be written up as an update/commit process which makes the
                // persistence atomic
                if (latestEvent < newEventTime)
                {
                    await persistenceProvider.PersistValue(lastEventTimestampKey, newEventTime.ToString("O"), cancellationToken);
                }
            }
            catch
            {
                log.Error("Failed to extract last event timestamp from the persistence provider.");
            }
        }

        async Task<Dictionary<string, Dictionary<string, List<DateTimeOffset>>>?> LoadFromPersistence(CancellationToken cancellationToken)
        {
            var eventContent = await persistenceProvider.GetValue("events", cancellationToken) ?? "";
            var configMapEvents = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<DateTimeOffset>>>>(eventContent);
            return configMapEvents;
        }

        async Task Persist(string key, Dictionary<string, List<DateTimeOffset>> jsonEntry, CancellationToken cancellationToken)
        {
            var jsonEncoded = JsonConvert.SerializeObject(jsonEntry);
            await persistenceProvider.PersistValue(key, jsonEncoded, cancellationToken);
        }
    }
}