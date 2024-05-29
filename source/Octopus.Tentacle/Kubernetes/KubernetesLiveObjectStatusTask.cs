using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesLiveObjectStatusTask : BackgroundTask
    {
        readonly KubernetesLiveObjectStatusService podMonitor;

        public KubernetesLiveObjectStatusTask(KubernetesLiveObjectStatusService podMonitor, ISystemLog log) : base(log, TimeSpan.FromSeconds(30))
        {
            this.podMonitor = podMonitor;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            await podMonitor.StartAsync(cancellationToken);
        }
    }
}