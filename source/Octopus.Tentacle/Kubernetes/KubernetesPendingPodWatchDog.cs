using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Time;
using Octopus.Time;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPendingPodWatchdog
    {
        Task StartAsync(CancellationToken cancellationToken);
    }

    public class KubernetesPendingPodWatchdog : IKubernetesPendingPodWatchdog
    {
        readonly IKubernetesPodStatusProvider podStatusProvider;
        readonly ISystemLog log;
        readonly IClock clock;
        readonly ITentacleScriptLogProvider scriptLogProvider;

        static readonly TimeSpan RecheckTime = TimeSpan.FromMinutes(1);
        internal readonly TimeSpan? pendingPodsConsideredStuckAfterTimeSpan = KubernetesConfig.PendingPodsConsideredStuckAfterTimeSpan;

        public KubernetesPendingPodWatchdog(
            IKubernetesPodStatusProvider podStatusProvider,
            ISystemLog log,
            IClock clock,
            ITentacleScriptLogProvider scriptLogProvider)
        {
            this.podStatusProvider = podStatusProvider;
            this.log = log;
            this.clock = clock;
            this.scriptLogProvider = scriptLogProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (pendingPodsConsideredStuckAfterTimeSpan is null)
            {
                log.Verbose($"PendingPodWatchdog: Environment variable {KubernetesConfig.PendingPodsConsideredStuckAfterTimeSpanVariableName} is not set or is an invalid number, so watchdog will not start.");
                return;
            }
            
            const int maxDurationSeconds = 70;

            //We don't want the monitoring to ever stop
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                retry => TimeSpan.FromSeconds(ExponentialBackoff.GetDuration(retry, maxDurationSeconds)),
                (ex, duration) =>
                {
                    log.Error(ex, "PendingPodWatchdog: An unexpected error occured while running watchdog loop, re-running in: " + duration);
                });

            await policy.ExecuteAsync(async ct => await WatchPendingPodsLoop(pendingPodsConsideredStuckAfterTimeSpan.Value ,ct), cancellationToken);
        }

        async Task WatchPendingPodsLoop(TimeSpan consideredStuckTimeSpan, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                log.Verbose("PendingPodWatchdog: Checking for stuck pending pods");
                CheckForStuckPendingPods(consideredStuckTimeSpan);

                var nextCheckTime = clock.GetUtcTime() + RecheckTime;
                log.Verbose($"PendingPodWatchdog: Next check will happen at {nextCheckTime:O}");

                await Task.Delay(RecheckTime, cancellationToken);
            }
        }

        void CheckForStuckPendingPods(TimeSpan consideredStuckTimeSpan)
        {
            var allPods = podStatusProvider.GetAllTrackedScriptPods();
            var pendingPods = allPods
                .Where(p => p.State.Phase == TrackedScriptPodPhase.Pending)
                .Where(p => p.CreationTimestamp is not null)
                .ToList();

            log.Verbose($"PendingPodWatchdog: {pendingPods.Count} pending pods found");

            var now = clock.GetUtcTime();
            
            //calculate the time, where if the pending pods creation date is before this, then the pods is considered stuck
            var podsAreStuckBeforeThisTime = now - pendingPodsConsideredStuckAfterTimeSpan;
            foreach (var pendingPod in pendingPods)
            {
                //if the creation timestamp is before the check time, then mark the pod as completed (which will then trigger the end state of the 
                if (pendingPod.CreationTimestamp!.Value < podsAreStuckBeforeThisTime)
                {
                    log.Verbose($"PendingPodWatchdog: Pod {pendingPod.ScriptTicket} has been pending for more than {consideredStuckTimeSpan.Minutes} minutes");

                    var podScriptLog = scriptLogProvider.GetOrCreate(pendingPod.ScriptTicket);
                    podScriptLog.Error($"The Kubernetes Pod '{pendingPod.ScriptTicket.ToKubernetesScriptPodName()}' has been in the '{pendingPod.State.Phase}' for more than {consideredStuckTimeSpan.Minutes} minute.");
                    
                    //marks the pod as completed with a canceled exit code, which then lets the script orchestration handle it :)
                    pendingPod.MarkAsCompleted(ScriptExitCodes.CanceledExitCode, now);
                }
            }
        }
    }
}