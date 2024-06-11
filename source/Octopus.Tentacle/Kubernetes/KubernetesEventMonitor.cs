using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
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
        public delegate KubernetesEventMonitor Factory(string kubernetesNamespace);
        
        readonly IKubernetesAgentMetrics agentMetrics;
        readonly IKubernetesEventService eventService;
        readonly string kubernetesNamespace;
        readonly EventMapper[] eventMappers;

        public KubernetesEventMonitor(IKubernetesAgentMetrics agentMetrics, IKubernetesEventService eventService, string kubernetesNamespace, EventMapper[] eventMappers)
        {
            this.agentMetrics = agentMetrics;
            this.eventService = eventService;
            this.kubernetesNamespace = kubernetesNamespace;
            this.eventMappers = eventMappers;
        }

        public async Task CacheNewEvents(CancellationToken cancellationToken)
        {
            var allEvents = await eventService.FetchAllEventsAsync(kubernetesNamespace, cancellationToken) ?? new Corev1EventList(new List<Corev1Event>());

            var lastCachedEventTimeStamp = agentMetrics.GetLatestEventTimestamp();

            var unseenEvents = allEvents.Items.Where(e =>
            {
                var eventTimestamp = EventHelpers.GetLatestTimestampInEvent(e);
                return eventTimestamp.HasValue && eventTimestamp.Value.ToUniversalTime() > lastCachedEventTimeStamp;
            });
            
            foreach (var kEvent in unseenEvents)
            {
                var result = MapToRecordableMetric(kEvent);
                if (result is not null)
                {
                    agentMetrics.TrackEvent(result.Reason, result.Source, result.OccurredAt);
                }
            }
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