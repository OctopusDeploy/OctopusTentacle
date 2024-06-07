using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesEventMonitor
    {
        Task CacheNewEvents(CancellationToken cancellationToken);
    }

    public class KubernetesEventMonitor : IKubernetesEventMonitor
    {
        public delegate KubernetesEventMonitor InNamespace(string kubernetesNamespace);
        
        readonly IKubernetesAgentMetrics agentMetrics;
        readonly IKubernetesEventService eventService;
        readonly string kubernetesNamespace;

        public KubernetesEventMonitor(IKubernetesAgentMetrics agentMetrics, IKubernetesEventService eventService, string kubernetesNamespace)
        {
            this.agentMetrics = agentMetrics;
            this.eventService = eventService;
            this.kubernetesNamespace = kubernetesNamespace;
        }

        public async Task CacheNewEvents(CancellationToken cancellationToken)
        {
            var allEvents = await eventService.FetchAllEventsAsync(kubernetesNamespace, cancellationToken) ?? new Corev1EventList(new List<Corev1Event>());

            var lastCachedEventTimeStamp = agentMetrics.GetLatestEventTimestamp();

            var unseenEvents = allEvents.Items.Where(e =>
            {
                var eventTimestamp = GetLatestTimestampInEvent(e);
                return eventTimestamp.HasValue && eventTimestamp.Value.ToUniversalTime() > lastCachedEventTimeStamp;
            });
            
            foreach (var kEvent in unseenEvents)
            {
                if (IsRelevantForMetrics(kEvent))
                {
                    var eventTimestamp = GetLatestTimestampInEvent(kEvent)!.Value.ToUniversalTime();
                    agentMetrics.TrackEvent(kEvent.Reason, kEvent.Name(), eventTimestamp);
                }
            }
        }

        bool IsRelevantForMetrics(Corev1Event kubernetesEvent)
        {
            return IsNfsPodRestart(kubernetesEvent) || IsTentacleAgentPodRestart(kubernetesEvent) || IsStaleNfsEvent(kubernetesEvent);
        }

        bool IsStaleNfsEvent(Corev1Event kubernetesEvent)
        {
            return kubernetesEvent.Reason == "NfsWatchdogTimeout";
        }

        bool IsNfsPodRestart(Corev1Event kubernetesEvent)
        {
            var podLifecycleEventsOfInterest = new []{"Started", "Killing"};
            //TODO(tmm): having this magic event-name stored as a constant somewhere would be great.
            return podLifecycleEventsOfInterest.Contains(kubernetesEvent.Reason) && kubernetesEvent.Name().StartsWith("octopus-agent-nfs");
        }

        bool IsTentacleAgentPodRestart(Corev1Event kubernetesEvent)
        {
            var podLifecycleEventsOfInterest = new []{"Started", "Killing"};
            //TODO(tmm): having this magic event-name stored as a constant somewhere would be great.
            return podLifecycleEventsOfInterest.Contains(kubernetesEvent.Reason) && kubernetesEvent.Name().StartsWith("octopus-agent-tentacle");
        }

        DateTime? GetLatestTimestampInEvent(Corev1Event kEvent)
        {
            return new List<DateTime?>
                {
                    kEvent.EventTime,
                    kEvent.LastTimestamp,
                    kEvent.FirstTimestamp
                }.Where(dt => dt.HasValue)
                .OrderByDescending(dt => dt!.Value)
                .FirstOrDefault();
        }
    }
}