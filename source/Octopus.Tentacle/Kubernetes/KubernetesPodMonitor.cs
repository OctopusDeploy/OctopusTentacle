using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodMonitor
    {
        Task StartAsync(CancellationToken token);
    }

    public interface IKubernetesPodStatusProvider
    {
        ITrackedKubernetesPod? TryGetPodStatus(ScriptTicket scriptTicket);
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly KubernetesPodLogMonitor.Factory podLogMonitorFactory;
        readonly Dictionary<ScriptTicket, TrackedKubernetesPod> podStatusLookup = new();

        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log, KubernetesPodLogMonitor.Factory podLogMonitorFactory)
        {
            this.podService = podService;
            this.log = log;
            this.podLogMonitorFactory = podLogMonitorFactory;
        }

        async Task IKubernetesPodMonitor.StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //initially load all the pods and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                // We start the watch from the resource version we initially loaded.
                // This means we only receive events that occur after the resource version
                try
                {
                    await podService.WatchAllPods(initialResourceVersion, (type, pod) => OnNewEvent(type, pod, cancellationToken), ex =>
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
            //clear the status'
            podStatusLookup.Clear();

            var allPods = await podService.ListAllPods(cancellationToken);
            foreach (var pod in allPods.Items)
            {
                var status = new TrackedKubernetesPod(pod.GetScriptTicket(), podLogMonitorFactory, pod);
                status.UpdateState(pod);

                log.Verbose($"Preloaded pod {pod.Name()}. {status}");
                podStatusLookup[status.ScriptTicket] = status;
            }

            log.Verbose($"Preloaded {allPods.Items.Count} pod statuses. ResourceVersion: {allPods.ResourceVersion()}");

            //this is the resource version for the list. We use this to start the watch at this particular point
            return allPods.ResourceVersion();
        }

        //This is internal so it's accessible via unit tests
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

                        var newStatus = new TrackedKubernetesPod(pod.GetScriptTicket(), podLogMonitorFactory, pod);

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

                        //if we are in a running phase, start the log monitoring
                        if (pod.Status.Phase == "Running")
                        {
                            status.StartMonitoringLogs(cancellationToken);
                        }

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

                        log.Verbose($"Removed {type} pod {pod.Name()} status");

                        //if the pod is deleted, remove it
                        podStatusLookup.Remove(scriptTicket);

                        log.Verbose($"Stopped tracking pod {pod.Name()}");
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

        ITrackedKubernetesPod? IKubernetesPodStatusProvider.TryGetPodStatus(ScriptTicket scriptTicket)
            => podStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
    }

    public class TrackedKubernetesPod : ITrackedKubernetesPod
    {
        readonly KubernetesPodLogMonitor podLogMonitor;
        public ScriptTicket ScriptTicket { get; }

        public TrackedPodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public TrackedKubernetesPod(ScriptTicket ticket, KubernetesPodLogMonitor.Factory podLogMonitorFactory, V1Pod pod)
        {
            this.podLogMonitor = podLogMonitorFactory(pod, OnScriptFinished);
            ScriptTicket = ticket;
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
                    State = TrackedPodState.Succeeded;
                    ExitCode = 0;
                    break;
                case "Failed":
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