using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodMonitorTask : IBackgroundTask
    {}

    public class KubernetesPodMonitorTask: BackgroundTask, IKubernetesPodMonitorTask
    {
        readonly IKubernetesPodMonitor podMonitor;

        public KubernetesPodMonitorTask(IKubernetesPodMonitor podMonitor, ISystemLog log, IKubernetesAgentMetrics fakeMetrics) : base(log, TimeSpan.FromSeconds(30))
        {
            this.podMonitor = podMonitor;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            await podMonitor.StartAsync(cancellationToken);
        }
    }
}
