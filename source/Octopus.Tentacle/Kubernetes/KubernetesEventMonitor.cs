using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesEventMonitor
    {
        Task CacheNewEvents(CancellationToken cancellationToken);
    }

    public class KubernetesEventMonitor : IKubernetesEventMonitor
    {
        readonly ISystemLog log;
        readonly IKubernetesAgentMetrics agentMetrics;
        readonly IKubernetesEventService eventService;

        public KubernetesEventMonitor(ISystemLog log, IKubernetesAgentMetrics agentMetrics, IKubernetesEventService eventService)
        {
            this.log = log;
            this.agentMetrics = agentMetrics;
            this.eventService = eventService;
        }

        public async Task CacheNewEvents(CancellationToken cancellationToken)
        {
            var allEvents = await eventService.FetchAllEventsAsync(GetNamespace(), cancellationToken) ?? new Corev1EventList();

            var lastCachedEventTimeStamp = agentMetrics.GetLatestEventTimestamp();

            var unseenEvents = allEvents.Items.Where(e =>
            {
                var eventTimestamp = GetLatestTimestampInEvent(e);
                return eventTimestamp.HasValue && eventTimestamp.Value.ToUniversalTime() >= lastCachedEventTimeStamp;
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
            return IsNfsPodRestart(kubernetesEvent) || IsPodRestartDueToStaleNfs(kubernetesEvent);
        }

        bool IsPodRestartDueToStaleNfs(Corev1Event kubernetesEvent)
        {
            return kubernetesEvent.Reason == "NfsWatchdogTimeout";
        }

        bool IsNfsPodRestart(Corev1Event kubernetesEvent)
        {
            var podLifecycleEventsOfInterest = new []{"Started", "Killing"};
            //TODO(tmm): having this magic event-name stored as a constant somewhere would be great.
            return podLifecycleEventsOfInterest.Contains(kubernetesEvent.Reason) && kubernetesEvent.Name().StartsWith("octopus-agent-nfs");
        }

        DateTime? GetLatestTimestampInEvent(Corev1Event kEvent)
        {
            return new List<DateTime?>
                {
                    kEvent.EventTime,
                    kEvent.LastTimestamp,
                    kEvent.FirstTimestamp
                }.Where(dt => dt.HasValue)
                .OrderBy(dt => dt!.Value)
                .FirstOrDefault();
        }

        protected virtual string GetNamespace()
        {
            return KubernetesConfig.Namespace;
        }
    }
}