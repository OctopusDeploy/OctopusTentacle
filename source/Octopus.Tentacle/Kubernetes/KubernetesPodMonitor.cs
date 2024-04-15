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
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Time;
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
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly ITentacleScriptLogProvider scriptLogProvider;

        //This is for the Pods retrieved from Kubernetes API
        ConcurrentDictionary<ScriptTicket, TrackedScriptPod> podStatusLookup = new();
        
        //This is for the new Pods we've just created that might not appear on the Kubernetes API yet
        readonly ConcurrentDictionary<ScriptTicket, TrackedScriptPod> pendingPodStatusLookup = new();
        
        //Prevent giving false results when we are still loading for the first time 
        readonly ManualResetEventSlim initialLoadLock = new();
        
        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log, ITentacleScriptLogProvider scriptLogProvider)
        {
            this.podService = podService;
            this.log = log;
            this.scriptLogProvider = scriptLogProvider;
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
            status.MarkAsCompleted(exitCode, DateTimeOffset.UtcNow);
        }

        public void AddPendingPod(ScriptTicket scriptTicket, V1Pod createdPod)
        {
            WaitForInitialLoadToFinish();

            var trackedScriptPod = new TrackedScriptPod(scriptTicket);
            trackedScriptPod.Update(createdPod);
            pendingPodStatusLookup.GetOrAdd(scriptTicket, _ => trackedScriptPod);
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
            initialLoadLock.Reset();
            log.Verbose("Loading pod statuses");

            Stopwatch stopwatch = Stopwatch.StartNew();
            
            var newStatuses = new ConcurrentDictionary<ScriptTicket, TrackedScriptPod>();
            var allPods = await podService.ListAllPods(cancellationToken);
            foreach (var pod in allPods.Items)
            {
                var scriptTicket = pod.GetScriptTicket();
                if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                {
                    status = new TrackedScriptPod(scriptTicket);
                }
                status.Update(pod);

                log.Verbose($"Loaded pod {pod.Name()} ({status})");
                newStatuses[scriptTicket] = status;
            }

            // Updating a reference is an atomic operation
            // and we only add data to this dictionary
            // in this class so this is thread safe.
            podStatusLookup = newStatuses;
            
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

            try
            {
                log.Verbose($"Received {type} event for pod {pod.Name()}");

                var scriptTicket = pod.GetScriptTicket();
                
                switch (type)
                {
                    case WatchEventType.Added or WatchEventType.Modified:
                    {
                        var status = podStatusLookup.GetOrAdd(scriptTicket, st => new TrackedScriptPod(st));
                        status.Update(pod);
                        log.Verbose($"Updated pod {pod.Name()} status. {status}");

                        break;
                    }
                    case WatchEventType.Deleted:
                        pendingPodStatusLookup.TryRemove(scriptTicket, out _);

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
            }
            catch (Exception e)
            {
                log.Error(e, $"Failed to process event {type} for pod {pod.Name()}.");
            }
        }

        IList<ITrackedScriptPod> IKubernetesPodStatusProvider.GetAllTrackedScriptPods()
        {
            WaitForInitialLoadToFinish();

            //Ensuring pendingPodStatusLookup doesn't contain duplicates involves
            return podStatusLookup
                .Concat(pendingPodStatusLookup)
                .ToLookup(p => p.Key)
                .Select(g => (ITrackedScriptPod)g.First().Value) //Deduplicate
                .ToList();
        }

        public ITrackedScriptPod? TryGetTrackedScriptPod(ScriptTicket scriptTicket)
        {
            WaitForInitialLoadToFinish();

            var found = podStatusLookup.TryGetValue(scriptTicket, out var status);
            if (found)
                return status;

            found = pendingPodStatusLookup.TryGetValue(scriptTicket, out status);
            return found ? status : null;
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
        int? ExitCode { get; }
        ScriptTicket ScriptTicket { get; }
        DateTimeOffset? FinishedAt { get; }
        void MarkAsCompleted(int exitCode, DateTimeOffset finishedAt);
    }
    
    public class TrackedScriptPod : ITrackedScriptPod
    {
        //The tracked Pod can be updated by K8s status updates or from the EOS marker
        readonly object lockObject = new object();
        
        public ScriptTicket ScriptTicket { get; }

        public TrackedScriptPodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public DateTimeOffset? FinishedAt { get; private set; }

        public TrackedScriptPod(ScriptTicket ticket)
        {
            ScriptTicket = ticket;
            State = TrackedScriptPodState.Running;
        }

        public void Update(V1Pod pod)
        {
            lock (lockObject)
            {
                switch (pod.Status?.Phase)
                {
                    case PodPhases.Succeeded:
                        var succeededState = GetTerminatedState();
                        FinishedAt = GetFinishedAt(succeededState);
                        State = TrackedScriptPodState.Succeeded;
                        ExitCode = succeededState.ExitCode;
                        break;
                    case PodPhases.Failed:
                        var failedState = GetTerminatedState();
                        FinishedAt = GetFinishedAt(failedState);
                        State = TrackedScriptPodState.Failed;
                        ExitCode = failedState.ExitCode;
                        break;
                }
            }

            DateTimeOffset? GetFinishedAt(V1ContainerStateTerminated terminated)
            {
                var finishedAtDateTime = terminated.FinishedAt!;
                return new DateTimeOffset(finishedAtDateTime.Value, TimeSpan.Zero);
            }

            V1ContainerStateTerminated GetTerminatedState()
            {
                return pod.Status.ContainerStatuses.Single(c => c.Name == ScriptTicket.ToKubernetesScriptPobName()).State.Terminated;
            }
        }

        public void MarkAsCompleted(int exitCode, DateTimeOffset finishedAt)
        {
            lock (lockObject)
            {
                FinishedAt = finishedAt;
                State = exitCode == 0 ? TrackedScriptPodState.Succeeded : TrackedScriptPodState.Failed;
                ExitCode = exitCode;
            }
        }

        public override string ToString()
            => $"ScriptTicket: {ScriptTicket}, State: {State}, ExitCode: {ExitCode}";
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

    public enum TrackedScriptPodState
    {
        Running,
        Succeeded,
        Failed
    }

    public static class V1PodExtensions
    {
        public static ScriptTicket GetScriptTicket(this V1Pod pod) => new(pod.GetLabel(OctopusLabels.ScriptTicketId));
    }
}