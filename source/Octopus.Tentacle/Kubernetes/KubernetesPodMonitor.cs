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
        PodStatus? TryGetPodStatus(ScriptTicket scriptTicket);
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly Dictionary<ScriptTicket, PodStatus> podStatusLookup = new();

        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log)
        {
            this.podService = podService;
            this.log = log;
        }

        async Task IKubernetesPodMonitor.StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //initially load all the pods and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                // We start the watch from the resource version we initially loaded.
                // This means we only receive events that occur after the resource version
                await podService.WatchAllPods(initialResourceVersion, OnNewEvent, ex =>
                    {
                        log.Error(ex, "An unhandled error occured in monitoring the pods");
                    }, cancellationToken
                );
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
                var status = new PodStatus(pod.GetScriptTicket());
                status.Update(pod);

                log.Verbose($"Preloaded pod {pod.Name()}. {status}");
                podStatusLookup[status.ScriptTicket] = status;
            }

            log.Verbose($"Preloaded {allPods.Items.Count} pod statuses. ResourceVersion: {allPods.ResourceVersion()}");

            //this is the resource version for the list. We use this to start the watch at this particular point
            return allPods.ResourceVersion();
        }

        //This is internal so it's accessible via unit tests
        internal async Task OnNewEvent(WatchEventType type, V1Pod pod)
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
                        if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                        {
                            status = new PodStatus(pod.GetScriptTicket());
                            podStatusLookup[scriptTicket] = status;
                        }

                        status.Update(pod);
                        log.Verbose($"Updated pod {pod.Name()} status. {status}");

                        break;
                    }
                    case WatchEventType.Deleted:
                        log.Verbose($"Removed {type} pod {pod.Name()} status");

                        //if the pod is deleted, remove it
                        podStatusLookup.Remove(scriptTicket);
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

        PodStatus? IKubernetesPodStatusProvider.TryGetPodStatus(ScriptTicket scriptTicket)
            => podStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
    }

    public class PodStatus
    {
        public ScriptTicket ScriptTicket { get; }

        public PodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public PodStatus(ScriptTicket ticket)
        {
            ScriptTicket = ticket;
            State = PodState.Running;
        }

        public void Update(V1Pod pod)
        {
            switch (pod.Status?.Phase)
            {
                case "Succeeded":
                    State = PodState.Succeeded;
                    ExitCode = 0;
                    break;
                case "Failed":
                    State = PodState.Failed;

                    //find the status for the container
                    //we we can't determine the exit code from the pod container, just return 1
                    ExitCode = pod.Status?.ContainerStatuses?.FirstOrDefault()?.State?.Terminated?.ExitCode ?? 1;

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