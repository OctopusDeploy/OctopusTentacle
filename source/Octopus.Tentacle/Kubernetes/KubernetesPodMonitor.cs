using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Time;
using Octopus.Time;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodMonitor
    {
        Task StartAsync(CancellationToken token);
        void MarkAsCompleted(ScriptTicket scriptTicket, int podLogsExitCode);
        void AddPendingPod(ScriptTicket commandScriptTicket, V1Pod createdPod);
    }

    public interface IKubernetesPodStatusProvider
    {
        IList<ITrackedScriptPod> GetAllTrackedScriptPods();
        ITrackedScriptPod? TryGetTrackedScriptPod(ScriptTicket scriptTicket);
        Task WaitForScriptPodToStart(ScriptTicket scriptTicket, CancellationToken cancellationToken);
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly IClock clock;

        event EventHandler<V1Pod>? PodUpdatedEvent;
        
        ConcurrentDictionary<ScriptTicket, TrackedScriptPod> podStatusLookup = new();
        
        //Prevent giving false results when we are still loading for the first time 
        readonly ManualResetEventSlim initialLoadLock = new();
        readonly object statusLookupWriteLock = new();
        
        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log, ITentacleScriptLogProvider scriptLogProvider, IClock clock)
        {
            this.podService = podService;
            this.log = log;
            this.scriptLogProvider = scriptLogProvider;
            this.clock = clock;
        }

        async Task IKubernetesPodMonitor.StartAsync(CancellationToken cancellationToken)
        {
            const int maxDurationSeconds = 70;
            
            // We don't want the monitoring to ever stop
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                retry => TimeSpan.FromSeconds(ExponentialBackoff.GetDuration(retry, maxDurationSeconds)),
                (ex, duration) =>
                {
                    log.Error(ex, "An unexpected error occured while monitoring Pods, waiting for: " + duration);
                });

            await policy.ExecuteAsync(async ct => await UpdateLoop(ct), cancellationToken);
        }

        public void MarkAsCompleted(ScriptTicket scriptTicket, int exitCode)
        {
            var status = TryGetTrackedScriptPod(scriptTicket);
            if (status == null) 
                return;
            
            var text = $"Marking '{scriptTicket.TaskId}' as completed with exit code: '{exitCode}'";
            scriptLogProvider.GetOrCreate(scriptTicket).Verbose(text);
            log.Verbose(text);
            status.MarkAsCompleted(exitCode, clock.GetUtcTime());
        }

        public void AddPendingPod(ScriptTicket scriptTicket, V1Pod createdPod)
        {
            WaitForInitialLoadToFinish();

            var trackedScriptPod = new TrackedScriptPod(scriptTicket, clock) { MightNotExistInClusterYet = true };
            trackedScriptPod.Update(createdPod);
            lock (statusLookupWriteLock)
            {
                podStatusLookup.GetOrAdd(scriptTicket, _ => trackedScriptPod);
            }
        }

        async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Initially load all the pods and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                // We start the watch from the resource version we initially loaded.
                // This means we only receive events that occur after the resource version
                await podService.WatchAllPods(initialResourceVersion, OnNewEvent, ex =>
                    {
                        log.Error(ex, "An unhandled error occured while watching Pods");
                    }, cancellationToken
                );
            }
        }

        internal async Task<string> InitialLoadAsync(CancellationToken cancellationToken)
        {
           log.Verbose("Loading pod statuses");

           Stopwatch stopwatch = Stopwatch.StartNew();

           var newStatuses = new ConcurrentDictionary<ScriptTicket, TrackedScriptPod>();
           var allPods = await podService.ListAllPods(cancellationToken);
           foreach (var pod in allPods.Items)
           {
               var scriptTicket = pod.GetScriptTicket();
               var status = new TrackedScriptPod(scriptTicket, clock);
               status.Update(pod);

               log.Verbose($"Loaded pod {pod.Name()} ({status})");
               newStatuses[scriptTicket] = status;
           }

           //single collection, lock on writes
           lock (statusLookupWriteLock)
           {
               //Merge in Pods that were just created
               //We can be sure we haven't missed any Pods due to "statusLookupWriteLock"
               foreach (var entry in podStatusLookup.Where(t => t.Value.MightNotExistInClusterYet)) 
                   newStatuses.GetOrAdd(entry.Key, _ => entry.Value);

               // Updating a reference is an atomic operation
               // and we only add data to this dictionary
               // in this class so this is thread safe.
               podStatusLookup = newStatuses;

           }

           log.Verbose($"Loaded {allPods.Items.Count} pod statuses in {stopwatch.Elapsed}. ResourceVersion: {allPods.ResourceVersion()}");

           //This is to guard against giving wrong results on Tentacle startup
           initialLoadLock.Set();

           //this is the resource version for the list. We use this to start the watch at this particular point
           return allPods.ResourceVersion();
        }

        // This is internal so it's accessible to unit tests
        internal async Task OnNewEvent(WatchEventType type, V1Pod pod, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            lock (statusLookupWriteLock)
            {
                try
                {
                    log.Verbose($"Received {type} event for pod {pod.Name()}");

                    var scriptTicket = pod.GetScriptTicket();

                    switch (type)
                    {
                        case WatchEventType.Added or WatchEventType.Modified:
                            var trackedScriptPod = new TrackedScriptPod(scriptTicket, clock);
                            trackedScriptPod.Update(pod);
                            trackedScriptPod.MightNotExistInClusterYet = false;
                            
                            var status = podStatusLookup.AddOrUpdate(scriptTicket, _ => trackedScriptPod, (_, _) => trackedScriptPod);
                            log.Verbose($"Updated pod {pod.Name()} status. {status}");

                            break;
                        case WatchEventType.Deleted:
                            //if the pod is deleted, remove it
                            if (podStatusLookup.TryRemove(scriptTicket, out _))
                            {
                                log.Verbose($"Removed {type} pod {pod.Name()} status");
                            }
                            else
                            {
                                log.Warn($"Unable to remove {type} pod {pod.Name()} status");
                            }

                            break;
                        default:
                            log.Warn($"Received watch event type {type} for pod {pod.Name()}. Ignoring as we don't need it");
                            break;
                    }

                    PodUpdatedEvent?.Invoke(this, pod);
                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to process event {type} for pod {pod.Name()}.");
                }
            }
        }

        IList<ITrackedScriptPod> IKubernetesPodStatusProvider.GetAllTrackedScriptPods()
        {
            WaitForInitialLoadToFinish();
            
            return podStatusLookup.Values.Cast<ITrackedScriptPod>().ToList();
        }

        public ITrackedScriptPod? TryGetTrackedScriptPod(ScriptTicket scriptTicket)
        {
            WaitForInitialLoadToFinish();
            var found = podStatusLookup.TryGetValue(scriptTicket, out var status);
            return found ? status : null;
        }

        public async Task WaitForScriptPodToStart(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            using var semaphore = new SemaphoreSlim(0);
            var onPodUpdated = (EventHandler<V1Pod>)OnPodUpdated;
            PodUpdatedEvent += onPodUpdated;
            try
            {
                if (TryGetTrackedScriptPod(scriptTicket) is null or {State: {Phase: TrackedScriptPodPhase.Pending}})
                {
                    await semaphore.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                PodUpdatedEvent -= onPodUpdated;
            }

            void OnPodUpdated(object? _, V1Pod pod)
            {
                if (pod.GetScriptTicket() != scriptTicket) return;

                var trackedPod = TryGetTrackedScriptPod(scriptTicket);
                if (trackedPod is {State: {Phase: not TrackedScriptPodPhase.Pending}} &&
                    pod.Status.ContainerStatuses.All(s => s.State.Waiting is null))
                {
                    semaphore.Release();
                }
            }
        }

        void WaitForInitialLoadToFinish()
        {
            if (!initialLoadLock.Wait(TimeSpan.FromSeconds(60)))
            {
                throw new Exception("Timed out waiting for Pod status to be loaded");
            }
        }
    }

    public interface ITrackedScriptPod
    {
        TrackedScriptPodState State { get; }
        ScriptTicket ScriptTicket { get; }
        void MarkAsCompleted(int exitCode, DateTimeOffset finishedAt);
    }
    
    public class TrackedScriptPod : ITrackedScriptPod
    {
        readonly IClock clock;
        public ScriptTicket ScriptTicket { get; }

        public TrackedScriptPodState State { get; private set; }

        //We create a tracked Pod entry when creating the script Pod so we don't need to wait for the K8s watch event to come through
        public bool MightNotExistInClusterYet { get; set; }
        
        public TrackedScriptPod(ScriptTicket ticket, IClock clock)
        {
            this.clock = clock;
            ScriptTicket = ticket;
            State = TrackedScriptPodState.Pending();
        }

        public void Update(V1Pod pod)
        {
            var scriptContainerState = GetScriptContainerState();
            
            //If we can't find the container state, but the Pod exists, assume it's still creating/pending
            if (scriptContainerState == null)
            {
                State = TrackedScriptPodState.Pending();
            }
            else if (scriptContainerState.Waiting is not null)
            {
                State = TrackedScriptPodState.Pending();
            }
            else if (scriptContainerState.Running is not null)
            {
                State = TrackedScriptPodState.Running();
            }
            else if (scriptContainerState.Terminated is not null)
            {
                var terminated = scriptContainerState.Terminated;
                State = terminated.ExitCode == 0 
                    ? TrackedScriptPodState.Succeeded(terminated.ExitCode, GetFinishedAt(terminated)) 
                    : TrackedScriptPodState.Failed(terminated.ExitCode, GetFinishedAt(terminated));
            }

            DateTimeOffset GetFinishedAt(V1ContainerStateTerminated terminated)
            {
                //If we don't have a finished time, then just assume it's now. The time is only used to detect orphaned Pods
                return terminated.FinishedAt != null
                    ? new DateTimeOffset(terminated.FinishedAt.Value, TimeSpan.Zero)
                    : clock.GetUtcTime();
            }

            V1ContainerState? GetScriptContainerState()
            {
                return pod.Status?.ContainerStatuses?.SingleOrDefault(c => c.Name == ScriptTicket.ToKubernetesScriptPodName())?.State;
            }
        }

        public void MarkAsCompleted(int exitCode, DateTimeOffset finishedAt)
        {
            State = exitCode == 0 
                ? TrackedScriptPodState.Succeeded(exitCode, finishedAt) 
                : TrackedScriptPodState.Failed(exitCode, finishedAt);
        }

        public override string ToString()
        {
            var state = State;
            return $"ScriptTicket: {ScriptTicket}, State: {state.Phase}, ExitCode: {state.ExitCode}";
        }
    }

    //Pod lifecycle has these phases:
    //https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#pod-phase
    static class PodPhases
    {
        public const string Pending = "Pending";
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Unknown = "Unknown";
    }

    public record TrackedScriptPodState
    {
        TrackedScriptPodState()
        {
        }

        public static TrackedScriptPodState Pending()
        {
            return new TrackedScriptPodState()
            {
                Phase = TrackedScriptPodPhase.Pending
            };
        }
        
        public static TrackedScriptPodState Running()
        {
            return new TrackedScriptPodState()
            {
                Phase = TrackedScriptPodPhase.Running
            };
        }

        public static TrackedScriptPodState Succeeded(int exitCode, DateTimeOffset finishedAt)
        {
            return new TrackedScriptPodState()
            {
                Phase = TrackedScriptPodPhase.Succeeded,
                ExitCode = exitCode, 
                FinishedAt = finishedAt
            };
        }

        public static TrackedScriptPodState Failed(int exitCode, DateTimeOffset finishedAt)
        {
            return new TrackedScriptPodState()
            {
                Phase = TrackedScriptPodPhase.Failed,
                ExitCode = exitCode, 
                FinishedAt = finishedAt
            };
        }

        public TrackedScriptPodPhase Phase { get; private init; }
        public int? ExitCode { get; private init; }
        public DateTimeOffset? FinishedAt { get; private init; }
    }

    public enum TrackedScriptPodPhase
    {
        Pending,
        Running,
        Succeeded,
        Failed
    }

    public static class V1PodExtensions
    {
        public static ScriptTicket GetScriptTicket(this V1Pod pod) => new(pod.GetLabel(OctopusLabels.ScriptTicketId));
    }
}