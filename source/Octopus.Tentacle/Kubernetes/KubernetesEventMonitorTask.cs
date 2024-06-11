using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesEventMonitorTask : BackgroundTask
    {
        public delegate KubernetesEventMonitorTask Factory(IKubernetesEventMonitor eventMonitor);
        
        readonly IKubernetesEventMonitor eventMonitor;
        readonly ISystemLog log;
        readonly TimeSpan taskInterval = TimeSpan.FromMinutes(10);
        public KubernetesEventMonitorTask(ISystemLog log, IKubernetesEventMonitor eventMonitor) : base(log, TimeSpan.FromSeconds(30))
        {
            this.log = log;
            this.eventMonitor = eventMonitor;
        }
        
        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            if (!KubernetesConfig.MetricsIsEnabled)
            {
                log.Info("Event monitoring for agent metrics is not enabled.");
                return;
            }
            
            //We don't want the monitoring to ever stop
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                retry => taskInterval,
                (ex, duration) =>
                {
                    log.Warn($"KubernetesEventMonitor: {ex.Message}");
                    log.Warn("KubernetesEventMonitor: An unexpected error occured while running event caching loop, re-running in: " + duration);
                    log.Verbose(ex);
                });

            await policy.ExecuteAsync(async ct => await RunTaskAtCadence(ct), cancellationToken);
        }
        
        async Task RunTaskAtCadence(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await eventMonitor.CacheNewEvents(cancellationToken);
                await Task.Delay(taskInterval, cancellationToken);
            }            
        }
    }
}