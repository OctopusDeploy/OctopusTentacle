using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Time;
using Octopus.Time;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesOrphanedPodCleaner
    {
        Task StartAsync(CancellationToken cancellationToken);
    }

    public class KubernetesOrphanedPodCleaner : IKubernetesOrphanedPodCleaner
    {
        readonly IKubernetesPodStatusProvider podStatusProvider;
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly IClock clock;
        readonly TimeSpan initialDelay = TimeSpan.FromMinutes(1);
        internal readonly TimeSpan CompletedPodConsideredOrphanedAfterTimeSpan = KubernetesConfig.PodsConsideredOrphanedAfterTimeSpan;

        public KubernetesOrphanedPodCleaner(IKubernetesPodStatusProvider podStatusProvider, IKubernetesPodService podService, ISystemLog log, IClock clock)
        {
            this.podStatusProvider = podStatusProvider;
            this.podService = podService;
            this.log = log;
            this.clock = clock;
        }

        async Task IKubernetesOrphanedPodCleaner.StartAsync(CancellationToken cancellationToken)
        {
            const int maxDurationSeconds = 70;

            //We don't want the monitoring to ever stop
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                retry => TimeSpan.FromSeconds(ExponentialBackoff.GetDuration(retry, maxDurationSeconds)),
                (ex, duration) =>
                {
                    log.Error(ex, "OrphanedPodCleaner: An unexpected error occured while running clean up loop, re-running in: " + duration);
                });

            await policy.ExecuteAsync(async ct => await CleanupOrphanedPodsLoop(ct), cancellationToken);
        }

        async Task CleanupOrphanedPodsLoop(CancellationToken cancellationToken)
        {
            // We do a small initial delay to ensure that the first time we check,
            // it's actually possible to have orphaned pods.
            await Task.Delay(initialDelay, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Delay first because the time we check against on the PodStatus is generated in Tentacle.
                // So pods are only orphaned once this tentacle is at least the poll period long.
                await Task.Delay(CompletedPodConsideredOrphanedAfterTimeSpan, cancellationToken);

                log.Verbose("OrphanedPodCleaner: Checking for orphaned pods");
                await CheckForOrphanedPods(cancellationToken);

                var nextCheckTime = clock.GetUtcTime() + CompletedPodConsideredOrphanedAfterTimeSpan;
                log.Verbose($"OrphanedPodCleaner: Next check will happen at {nextCheckTime:O}");
            }
        }

        internal async Task CheckForOrphanedPods(CancellationToken cancellationToken)
        {
            var cutOffDateTime = clock.GetUtcTime() - CompletedPodConsideredOrphanedAfterTimeSpan;
            var allPods = podStatusProvider.GetAllPodStatuses();
            var orphanedPods = allPods.Where(p => p.State != PodState.Running && p.LastUpdated <= cutOffDateTime).ToList();

            if (orphanedPods.Count == 0)
            {
                log.Verbose("OrphanedPodCleaner: No orphaned pods found");
                return;
            }

            log.Info($"OrphanedPodCleaner: Found {orphanedPods.Count} orphaned pods, they will now be deleted");
            foreach (var pod in orphanedPods)
            {
                if (KubernetesConfig.DisableAutomaticPodCleanup)
                {
                    log.Verbose($"OrphanedPodCleaner: Not deleting orphaned pod {pod.ScriptTicket} as automatic cleanup is disabled");
                    continue;
                }

                try
                {
                    log.Verbose($"OrphanedPodCleaner: Deleting orphaned pod: {pod.ScriptTicket}");
                    await podService.Delete(pod.ScriptTicket, cancellationToken);
                }
                catch
                {
                    log.Warn($"OrphanedPodCleaner: Unable to delete orphaned pod: {pod.ScriptTicket}, will try again next check");
                }
            }
        }
    }
}