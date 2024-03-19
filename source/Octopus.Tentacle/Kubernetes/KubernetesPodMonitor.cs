using System;
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
        IList<PodStatus> GetAllPodStatuses();
        PodStatus? TryGetPodStatus(ScriptTicket scriptTicket);
    }

    public class KubernetesPodMonitor : IKubernetesPodMonitor, IKubernetesPodStatusProvider
    {
        readonly IKubernetesPodService podService;
        readonly ISystemLog log;
        readonly IClock clock;
        readonly Dictionary<ScriptTicket, PodStatus> podStatusLookup = new();
        readonly SemaphoreSlim lookupLock = new(1, 1);

        public KubernetesPodMonitor(IKubernetesPodService podService, ISystemLog log, IClock clock)
        {
            this.podService = podService;
            this.log = log;
            this.clock = clock;
        }

        async Task IKubernetesPodMonitor.StartAsync(CancellationToken cancellationToken)
        {
            const int maxDurationSeconds = 70;
            
            //We don't want the monitoring to ever stop
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
                //initially load all the pods and their status's
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
            await lookupLock.WaitAsync(cancellationToken);
            try
            {
                log.Verbose("Preloading pod statuses");
                var oldLookup = podStatusLookup.ToDictionary(x => x.Key, x => x.Value);
                //clear the status'
                podStatusLookup.Clear();

                var allPods = await podService.ListAllPodsAsync(cancellationToken);
                foreach (var pod in allPods.Items)
                {
                    var scriptTicket = pod.GetScriptTicket();
                    if (!oldLookup.TryGetValue(scriptTicket, out var status))
                    {
                        status = new PodStatus(pod.GetScriptTicket(), clock);
                    }
                    status.Update(pod);

                    log.Verbose($"Preloaded pod {pod.Name()}. {status}");
                    podStatusLookup[status.ScriptTicket] = status;
                }

                log.Verbose($"Preloaded {allPods.Items.Count} pod statuses. ResourceVersion: {allPods.ResourceVersion()}");

                //this is the resource version for the list. We use this to start the watch at this particular point
                return allPods.ResourceVersion();
            }
            finally
            {
                lookupLock.Release();
            }
        }

        //This is internal so it's accessible via unit tests
        internal async Task OnNewEvent(WatchEventType type, V1Pod pod, CancellationToken cancellationToken)
        {
            try
            {
                log.Verbose($"Received {type} event for pod {pod.Name()}");

                var scriptTicket = pod.GetScriptTicket();
                await lookupLock.WaitAsync(cancellationToken);
                try
                {
                    switch (type)
                    {
                        case WatchEventType.Added or WatchEventType.Modified:
                        {
                            if (!podStatusLookup.TryGetValue(scriptTicket, out var status))
                            {
                                podStatusLookup[scriptTicket] = status = new PodStatus(scriptTicket, clock);
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
                finally
                {
                    lookupLock.Release();
                }
            }
            catch (Exception e)
            {
                log.Error(e, $"Failed to process event {type} for pod {pod.Name()}.");
            }
        }

        IList<PodStatus> IKubernetesPodStatusProvider.GetAllPodStatuses()
        {
            lookupLock.Wait();
            try
            {
                return podStatusLookup.Values.ToList();
            }
            finally
            {
                lookupLock.Release();
            }
        }

        PodStatus? IKubernetesPodStatusProvider.TryGetPodStatus(ScriptTicket scriptTicket)
        {
            lookupLock.Wait();
            try
            {
                return podStatusLookup.TryGetValue(scriptTicket, out var status) ? status : null;
            }
            finally
            {
                lookupLock.Release();
            }
        }
    }

    public class PodStatus
    {
        readonly IClock clock;
        public ScriptTicket ScriptTicket { get; }

        public PodState State { get; private set; }

        public int? ExitCode { get; private set; }

        public DateTimeOffset LastUpdated { get; private set; }

        public PodStatus(ScriptTicket ticket, IClock clock)
        {
            this.clock = clock;
            ScriptTicket = ticket;
            State = PodState.Running;
            LastUpdated = clock.GetUtcTime();
        }

        public void Update(V1Pod pod)
        {
            switch (pod.Status?.Phase)
            {
                case "Succeeded":
                    if (State != PodState.Succeeded) LastUpdated = clock.GetUtcTime();
                    State = PodState.Succeeded;
                    ExitCode = 0;
                    break;
                case "Failed":
                    if (State != PodState.Failed) LastUpdated = clock.GetUtcTime();
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