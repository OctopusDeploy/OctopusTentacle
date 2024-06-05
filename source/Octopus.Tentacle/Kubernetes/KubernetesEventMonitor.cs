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
        Task StartAsync(CancellationToken cancellationToken);
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //We don't want the monitoring to ever stop
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                    retry => TimeSpan.FromMinutes(10),
            (ex, duration) =>
            {
                log.Error(ex, "KubernetesEventMonitor: An unexpected error occured while running event caching loop, re-running in: " + duration);
            });

            await policy.ExecuteAsync(async ct => await CacheNewEvents(ct), cancellationToken);
        }

        async Task CacheNewEvents(CancellationToken cancellationToken)
        {
            var allEvents = await eventService.FetchAllEventsAsync(GetNamespace(), cancellationToken);

            var lastCachedEvent = agentMetrics.GetLatestEventTimestamp();

            var unseenEvents = allEvents?.Items.Where(e => e.EventTime.HasValue && e.EventTime.Value.ToUniversalTime() > lastCachedEvent);
            
            foreach (var kEvent in unseenEvents!)
            {
                if (IsRelevantForMetrics(kEvent))
                {
                    agentMetrics.TrackEvent(kEvent.Reason, kEvent.Name(), kEvent.EventTime!.Value.ToUniversalTime());
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
            var restartReason = new []{"Started", "Killing"};
            return restartReason.Contains(kubernetesEvent.Reason) && kubernetesEvent.Name().StartsWith("octopus-agent-nfs");
        }


        protected virtual string GetNamespace()
        {
            return KubernetesConfig.Namespace;
        }
    }
}