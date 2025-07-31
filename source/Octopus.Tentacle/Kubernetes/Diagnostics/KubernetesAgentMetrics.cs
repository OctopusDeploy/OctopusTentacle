using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Diagnostics
{
    using MetricsDictionary = Dictionary<string, Dictionary<string, List<DateTimeOffset>>>;
    public interface IKubernetesAgentMetrics
    {
        Task TrackEvent(string reason, string source, DateTimeOffset occurrence, CancellationToken cancellationToken);
        Task<DateTimeOffset> GetLatestEventTimestamp(CancellationToken cancellationToken);
    }
    
    public class KubernetesAgentMetrics : IKubernetesAgentMetrics
    {
        const string LastEventTimestampKey = "latestTimestamp";

        public delegate KubernetesAgentMetrics Factory(IPersistenceProvider persistenceProvider, string configMapKey);

        readonly SemaphoreSlim semaphore = new(1);
        readonly IPersistenceProvider persistenceProvider;
        readonly string configMapKey;
        readonly ISystemLog log;

        public KubernetesAgentMetrics(IPersistenceProvider persistenceProvider, string configMapKey, ISystemLog log)
        {
            this.persistenceProvider = persistenceProvider;
            this.configMapKey = configMapKey;
            this.log = log;
        }

        public async Task TrackEvent(string reason, string source, DateTimeOffset occurrence, CancellationToken cancellationToken)
        {
            try
            {
                using var _ = await semaphore.LockAsync(cancellationToken);
                await PerformUpdateOnMetricsForReason(reason, reasonDictionary =>
                {
                    if (!reasonDictionary.TryGetValue(source, out var occurenceTimestamps))
                    {
                        reasonDictionary[source] = occurenceTimestamps = new List<DateTimeOffset>();
                    }
                    occurenceTimestamps.Add(occurrence);
                }, cancellationToken);
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
            var timeStampString = await persistenceProvider.GetValue(LastEventTimestampKey, cancellationToken);
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
                var latestEvent = await GetLatestEventTimeStampInternal(cancellationToken);
                // The config map should _probably_ be written up as an update/commit process which makes the
                // persistence atomic
                if (latestEvent < newEventTime)
                {
                    await persistenceProvider.PersistValue(LastEventTimestampKey, newEventTime.ToString("O"), cancellationToken);
                }
            }
            catch
            {
                log.Error("Failed to extract last event timestamp from the persistence provider.");
            }
        }

        async Task PerformUpdateOnMetricsForReason(string reason, Action<Dictionary<string, List<DateTimeOffset>>> updateAction, CancellationToken cancellationToken)
        {
            var metrics = await LoadFromPersistence(cancellationToken) ?? new MetricsDictionary();
            if (!metrics.TryGetValue(reason, out var reasonDictionary))
            {
                metrics[reason] = reasonDictionary = new Dictionary<string, List<DateTimeOffset>>();
            }

            updateAction(reasonDictionary);

            await Persist(metrics, cancellationToken);
        }

        async Task<MetricsDictionary?> LoadFromPersistence(CancellationToken cancellationToken)
        {
            var eventContent = await persistenceProvider.GetValue(configMapKey, cancellationToken) ?? "";
            var configMapEvents = JsonConvert.DeserializeObject<MetricsDictionary>(eventContent);
            return configMapEvents;
        }

        async Task Persist(MetricsDictionary jsonEntry, CancellationToken cancellationToken)
        {
            var jsonEncoded = JsonConvert.SerializeObject(jsonEntry);
            await persistenceProvider.PersistValue(configMapKey, jsonEncoded, cancellationToken);
        }
    }
}