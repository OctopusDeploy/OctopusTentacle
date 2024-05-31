using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesEventMonitorTask : BackgroundTask
    {
        readonly KubernetesEventMonitor eventMonitor;
        public KubernetesEventMonitorTask(ISystemLog log, TimeSpan terminationGracePeriod, KubernetesEventMonitor eventMonitor) : base(log, terminationGracePeriod)
        {
            this.eventMonitor = eventMonitor;
        }
        
        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            await eventMonitor.StartAsync(cancellationToken);
        }
    }
}