using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
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
        readonly KubernetesAgentMetrics agentMetrics;
        readonly KubernetesEventService eventService;

        public KubernetesEventMonitor(ISystemLog log, KubernetesAgentMetrics agentMetrics, KubernetesEventService eventService)
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
            var allEvents = await eventService.FetchAllEventsAsync(cancellationToken);
            if (allEvents is null)
            {
                log.Error("Unable to extract events from the cluster");
                return;
            }
            
            DateTimeOffset lastEventTime = default;
            try
            {
                lastEventTime = agentMetrics.GetLatestEventTimestamp();
            }
            catch (Exception e)
            {
                log.Error($"Failed to determine latest handled event. {e.Message}");
            }

            var unSeenEvents = allEvents.Items
                .Where(e => e.EventTime.HasValue && e.EventTime.Value.ToUniversalTime() > lastEventTime);
            
            foreach (var unSeenEvent in unSeenEvents)
            {
                var eventRecrd = MapToEventRecord(unSeenEvent);
                agentMetrics.TrackEvent(new EventRecord(unSeenEvent.Action, unSeenEvent.Source.Component, unSeenEvent.EventTime!.Value.ToUniversalTime()));
            }

            await Task.CompletedTask;
        }

        EventRecord? MapToEventRecord(Corev1Event kubernetesEvent)
        {
            if (kubernetesEvent.Action.Equals("Killing", StringComparison.OrdinalIgnoreCase))
            {
                return new EventRecord(kubernetesEvent.Action, kubernetesEvent.Source.Component, kubernetesEvent.EventTime!.Value.ToUniversalTime());
            }

            return null;
        }
    }
}