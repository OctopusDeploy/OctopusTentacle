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
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodMonitor
    {
        Task StartAsync(CancellationToken token);
    }

    public interface IKubernetesPodStatusProvider
    {
        IList<PodStatus> GetAllPodStatuses();
        PodStatus? TryGetPodStatus(ScriptTicket scriptTicket);
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;

        ConcurrentDictionary<ScriptTicket, PodStatus> podStatusLookup = new();

        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log)
        {
            this.podService = podService;
            this.log = log;
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
                await podService.WatchAllPods(initialResourceVersion, OnNewEvent, ex =>
                    {
                        log.Error(ex, "An unhandled error occured while watching Pods");
                    }, cancellationToken
                );
            }
        }

        internal async Task<string> InitialLoadAsync(CancellationToken cancellationToken)
        {
            log.Verbose("Preloading pod statuses");

            var newStatuses = new ConcurrentDictionary<ScriptTicket, PodStatus>();
            var allPods = await podService.ListAllPodsAsync(cancellationToken);
            foreach (var pod in allPods.Items)
            {
                var scriptTicket = pod.GetScriptTicket();
                if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                {
                    status = new PodStatus(scriptTicket);
                }
                status.Update(pod);

                log.Verbose($"Preloaded pod {pod.Name()}. {status}");
                newStatuses[scriptTicket] = status;
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
                    case WatchEventType.Added or WatchEventType.Modified:
                    {
                        var status = podStatusLookup.GetOrAdd(scriptTicket, st => new PodStatus(st));
                        status.Update(pod);
                        log.Verbose($"Updated pod {pod.Name()} status. {status}");

                        break;
                    }
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
            }
            catch (Exception e)
            {
                log.Error(e, $"Failed to process event {type} for pod {pod.Name()}.");
            }
        }

        IList<PodStatus> IKubernetesPodStatusProvider.GetAllPodStatuses() =>
            podStatusLookup.Values.ToList();

        PodStatus? IKubernetesPodStatusProvider.TryGetPodStatus(ScriptTicket scriptTicket) =>
            podStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
    }

    public class PodStatus
    {
        public ScriptTicket ScriptTicket { get; }

        public PodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public DateTimeOffset? FinishedAt { get; private set; }

        public PodStatus(ScriptTicket ticket)
        {
            ScriptTicket = ticket;
            State = PodState.Running;
        }

        public void Update(V1Pod pod)
        {
            var terminatedState = pod.Status?.ContainerStatuses.FirstOrDefault(c => c.Name == ScriptTicket.ToKubernetesScriptPobName())?.State.Terminated;

            DateTimeOffset? GetFinishedAt()
            {
                var finishedAtDateTime = terminatedState?.FinishedAt;
                return finishedAtDateTime is not null ? new DateTimeOffset(finishedAtDateTime.Value, TimeSpan.Zero) : null;
            }

            switch (pod.Status?.Phase)
            {
                case "Succeeded":
                    FinishedAt = GetFinishedAt();
                    State = PodState.Succeeded;
                    ExitCode = 0;
                    break;
                case "Failed":
                    FinishedAt = GetFinishedAt();
                    State = PodState.Failed;

                    //find the status for the container
                    //if we can't determine the exit code from the pod container, just return 1
                    ExitCode = terminatedState?.ExitCode ?? 1;
                    break;
            }
        }

        public override string ToString()
            => $"ScriptTicket: {ScriptTicket}, State: {State}, ExitCode: {ExitCode}";
    }

    public enum PodState
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