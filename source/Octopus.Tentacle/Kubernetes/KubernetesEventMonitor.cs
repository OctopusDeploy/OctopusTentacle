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
                var eventRecord = MapToEventRecord(kEvent);
                if (eventRecord is not null)
                {
                    agentMetrics.TrackEvent(eventRecord.Reason, eventRecord.Source, eventRecord.FirstOccurrence);    
                }
            }
        }
        
        EventRecord? MapToEventRecord(Corev1Event kubernetesEvent)
        {
            //only want to monitor 2 types of event
            string? source;

            //TODO(tmm): Don't love the hard-coded constants here - there needs to be a less brittle solution.
            if (kubernetesEvent.Name().StartsWith(KubernetesScriptPodNameExtensions.OctopusScriptPodNamePrefix))
            {
                source = KubernetesScriptPodNameExtensions.OctopusScriptPodNamePrefix;
            }
            else if(kubernetesEvent.Name().StartsWith("octopus-agent-nfs"))
            {
                source = "octopus-agent-nfs";
            }
            else if (kubernetesEvent.Name().StartsWith("octopus-agent-tentacle"))
            {
                source = "octopus-agent-tentacle";
            }
            else
            {
                return null;
            }
            
            return new EventRecord(kubernetesEvent.Reason, source, kubernetesEvent.EventTime!.Value.ToUniversalTime());
        }

        protected virtual string GetNamespace()
        {
            return KubernetesConfig.Namespace;
        }
    }

    record EventRecord(string Reason, string Source, DateTimeOffset FirstOccurrence);
}