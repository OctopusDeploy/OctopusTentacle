using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesOrphanedPodCleanerTask : IBackgroundTask
    {}

    public class KubernetesOrphanedPodCleanerTask : BackgroundTask, IKubernetesOrphanedPodCleanerTask
    {
        readonly IKubernetesOrphanedPodCleaner podCleaner;

        public KubernetesOrphanedPodCleanerTask(IKubernetesOrphanedPodCleaner podCleaner, ISystemLog log)
            : base(log, TimeSpan.FromSeconds(30))
        {
            this.podCleaner = podCleaner;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
            await podCleaner.StartAsync(cancellationToken);
        }
    }
}