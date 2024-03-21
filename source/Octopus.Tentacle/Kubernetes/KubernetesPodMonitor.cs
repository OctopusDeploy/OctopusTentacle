using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    }

    public interface IKubernetesPodStatusProvider
    {
        ITrackedKubernetesPod? TryGetPodStatus(ScriptTicket scriptTicket);
        IList<ITrackedKubernetesPod> GetAllPodStatuses();
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly KubernetesPodLogMonitor.Factory podLogMonitorFactory;
        readonly IClock clock;
        ConcurrentDictionary<ScriptTicket, TrackedKubernetesPod> podStatusLookup = new();

        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log, KubernetesPodLogMonitor.Factory podLogMonitorFactory, IClock clock)
        {
            this.podService = podService;
            this.log = log;
            this.clock = clock;
            this.podLogMonitorFactory = podLogMonitorFactory;
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

        async Task UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Initially load all the pods and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                // We start the watch from the resource version we initially loaded.
                // This means we only receive events that occur after the resource version
                try
                {
                    await podService.WatchAllPods(initialResourceVersion, OnNewEvent, ex =>
                        {
                            log.Error(ex, "An error occurred retrieving the pod watch result.");
                        }, cancellationToken
                    );
                }
                catch (Exception e)
                {
                    log.Warn(e, "An unhandled exception occurred during WatchAllPods.");
                }
            }
        }

        internal async Task<string> InitialLoadAsync(CancellationToken cancellationToken)
        {
            log.Verbose("Preloading pod statuses");

            var newStatuses = new ConcurrentDictionary<ScriptTicket, TrackedKubernetesPod>();
            var allPods = await podService.ListAllPods(cancellationToken);
            foreach (var pod in allPods.Items)
            {
                var scriptTicket = pod.GetScriptTicket();
                if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                {
                    status = new TrackedKubernetesPod(scriptTicket, podLogMonitorFactory, pod, clock);
                }
                status.UpdateState(pod);

                log.Verbose($"Preloaded pod {pod.Name()}. {status}");
                newStatuses[scriptTicket] = status;

                //start monitoring logs for all existing pods
                status.StartMonitoringLogs(cancellationToken);
            }

            // Updating a reference is an atomic operation
            // and we only add data to this dictionary
            // in this class so this is thread safe.
            podStatusLookup = newStatuses;

            log.Verbose($"Preloaded {allPods.Items.Count} pod statuses. ResourceVersion: {allPods.ResourceVersion()}");

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
                    case WatchEventType.Added:
                    {
                        if (podStatusLookup.ContainsKey(scriptTicket))
                        {
                            log.Warn($"Pod status for pod {pod.Name()} is already being tracked, but an Added event was received.");
                            return;
                        }

                        var newStatus = new TrackedKubernetesPod(pod.GetScriptTicket(), podLogMonitorFactory, pod, clock);

                        log.Verbose($"Starting tracking pod {pod.Name()}. {newStatus}");
                        podStatusLookup[scriptTicket] = newStatus;

                        newStatus.StartMonitoringLogs(cancellationToken);

                        break;
                    }
                    case WatchEventType.Modified:
                    {
                        if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                        {
                            log.Warn($"Pod status for pod {pod.Name()} is not being tracked, but a Modified event was received.");
                            return;
                        }
                        status.UpdateState(pod);
                        log.Verbose($"Updated pod {pod.Name()} status. {status}");

                        break;
                    }
                    case WatchEventType.Deleted:
                    {
                        if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                        {
                            log.Warn($"Pod status for pod {pod.Name()} is not being tracked, but a Deleted event was received.");
                            return;
                        }

                        status.StopMonitoringLogs();

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
                    }
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

        IList<ITrackedKubernetesPod> IKubernetesPodStatusProvider.GetAllPodStatuses() =>
            podStatusLookup.Values.Cast<ITrackedKubernetesPod>().ToList();

        ITrackedKubernetesPod? IKubernetesPodStatusProvider.TryGetPodStatus(ScriptTicket scriptTicket)
            => podStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
    }

    public class TrackedKubernetesPod : ITrackedKubernetesPod
    {
        readonly IClock clock;
        readonly KubernetesPodLogMonitor podLogMonitor;
        public ScriptTicket ScriptTicket { get; }

        public TrackedPodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public DateTimeOffset LastUpdated { get; private set; }

        public TrackedKubernetesPod(ScriptTicket ticket, KubernetesPodLogMonitor.Factory podLogMonitorFactory, V1Pod pod, IClock clock)
        {
            this.clock = clock;
            podLogMonitor = podLogMonitorFactory(pod, OnScriptFinished);
            ScriptTicket = ticket;
            LastUpdated = clock.GetUtcTime();
            State = TrackedPodState.Running;
        }

        public void StartMonitoringLogs(CancellationToken cancellationToken) => podLogMonitor.StartMonitoring(cancellationToken);

        public void StopMonitoringLogs() => podLogMonitor.StopMonitoring();

        public void UpdateState(V1Pod pod)
        {
            //if we are already finished
            if (State is not TrackedPodState.Running)
                return;

            switch (pod.Status?.Phase)
            {
                case "Succeeded":
                    if (State != TrackedPodState.Succeeded) LastUpdated = clock.GetUtcTime();
                    State = TrackedPodState.Succeeded;
                    ExitCode = 0;
                    break;
                case "Failed":
                    if (State != TrackedPodState.Failed) LastUpdated = clock.GetUtcTime();
                    State = TrackedPodState.Failed;

                    //find the status for the container
                    //we we can't determine the exit code from the pod container, just return 1
                    ExitCode = pod.Status?.ContainerStatuses?.FirstOrDefault()?.State?.Terminated?.ExitCode ?? 1;
                    break;
            }
        }

        public (long NewSequence, List<PodLogLine> LogLines) GetLogs(long lastLogSequence)
            => podLogMonitor.GetLogs(lastLogSequence);

        void OnScriptFinished(TrackedPodState state, int exitCode)
        {
            State = state;
            ExitCode = exitCode;
        }

        public override string ToString()
            => $"ScriptTicket: {ScriptTicket}, State: {State}, ExitCode: {ExitCode}";
    }

    public interface ITrackedKubernetesPod
    {
        (long NewSequence, List<PodLogLine> LogLines) GetLogs(long lastLogSequence);
        TrackedPodState State { get; }
        int? ExitCode { get; }
        ScriptTicket ScriptTicket { get; }
        public DateTimeOffset LastUpdated { get; }
    }

    public enum TrackedPodState
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