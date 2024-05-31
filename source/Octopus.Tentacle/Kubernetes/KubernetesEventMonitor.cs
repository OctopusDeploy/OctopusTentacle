using System;
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
        //Needs the KubernetesMetric interface

        public KubernetesEventMonitor(ISystemLog log, KubernetesAgentMetrics agentMetrics)
        {
            this.log = log;
            this.agentMetrics = agentMetrics;
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
            V1EventSource eventSource; 
            
            await Task.CompletedTask;
        }
    }
}