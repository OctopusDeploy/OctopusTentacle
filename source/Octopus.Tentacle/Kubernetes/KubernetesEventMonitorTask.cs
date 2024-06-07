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
        readonly IKubernetesEventMonitor eventMonitor;
        readonly ISystemLog log;
        public KubernetesEventMonitorTask(ISystemLog log, TimeSpan terminationGracePeriod, IKubernetesEventMonitor eventMonitor) : base(log, terminationGracePeriod)
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
                retry => TimeSpan.FromMinutes(10),
                (ex, duration) =>
                {
                    log.Error(ex, "KubernetesEventMonitor: An unexpected error occured while running event caching loop, re-running in: " + duration);
                });

            await policy.ExecuteAsync(async ct => await eventMonitor.CacheNewEvents(ct), cancellationToken);
        }
    }
}