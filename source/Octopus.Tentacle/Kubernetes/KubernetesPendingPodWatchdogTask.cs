using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesStuckPodWatchdogTask : IBackgroundTask
    {
    }

    public class KubernetesStuckPodWatchdogTask : BackgroundTask, IKubernetesStuckPodWatchdogTask
    {
        readonly IKubernetesPendingPodWatchdog watchdog;

        public KubernetesStuckPodWatchdogTask(IKubernetesPendingPodWatchdog watchdog, ISystemLog log)
            : base(log, TimeSpan.FromSeconds(30))
        {
            this.watchdog = watchdog;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            await watchdog.StartAsync(cancellationToken);
        }
    }
}