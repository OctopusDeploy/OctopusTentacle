using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesEventMonitor
    {
        Task CacheNewEvents(CancellationToken cancellationToken);
    }

    public class KubernetesEventMonitor : IKubernetesEventMonitor
    {
        readonly IKubernetesConfiguration kubernetesConfiguration;
        readonly IKubernetesAgentMetrics agentMetrics;
        readonly IKubernetesEventService eventService;
        readonly IEventMapper[] eventMappers;
        readonly ISystemLog log;

        public KubernetesEventMonitor(IKubernetesConfiguration kubernetesConfiguration, IKubernetesAgentMetrics agentMetrics, IKubernetesEventService eventService, IEventMapper[] eventMappers, ISystemLog log)
        {
            this.kubernetesConfiguration = kubernetesConfiguration;
            this.agentMetrics = agentMetrics;
            this.eventService = eventService;
            this.eventMappers = eventMappers;
            this.log = log;
        }

        public async Task CacheNewEvents(CancellationToken cancellationToken)
        {
            log.Info($"Parsing kubernetes event list for namespace {kubernetesConfiguration.Namespace}.");
            var allEvents = await eventService.FetchAllEventsAsync(cancellationToken) ?? new Corev1EventList(new List<Corev1Event>());

            var lastCachedEventTimeStamp = await agentMetrics.GetLatestEventTimestamp(cancellationToken);

            var unseenEvents = allEvents.Items.Where(e =>
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(e);
                return eventTimestamp.HasValue && eventTimestamp.Value.ToUniversalTime() > lastCachedEventTimeStamp;
            }).ToImmutableList();
            
            log.Info($"Found {unseenEvents.Count} events since last logged event {lastCachedEventTimeStamp:O}");
            int trackedEventCount = 0;
            foreach (var kEvent in unseenEvents)
            {
                var result = MapToRecordableMetric(kEvent);
                if (result is not null)
                {
                    await agentMetrics.TrackEvent(result.Reason, result.Source, result.OccurredAt, cancellationToken);
                    trackedEventCount++;
                }
            }
            log.Info($"Added {trackedEventCount} entries to agent metrics.");
        }

        EventRecord? MapToRecordableMetric(Corev1Event kubernetesEvent)
        {
            foreach (var eventMapper in eventMappers)
            {
                var result = eventMapper.MapToRecordableEvent(kubernetesEvent);
                if (result is not null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}