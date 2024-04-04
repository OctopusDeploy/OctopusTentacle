using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests
{
    public class KubeLogFixture
    {
        k8s.Kubernetes client;
        SystemLog log;

        [SetUp]
        public void SetUp()
        {
            client = new k8s.Kubernetes(new LocalMachineKubernetesClientConfigProvider().Get());
            log = new SystemLog();
        }

        [Test]
        public async Task GetLogsSinceTime()
        {
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE", "octopus-agent-aksadmin");

            await StartAsync(CancellationToken.None);

        }

        readonly Dictionary<ScriptTicket, PodStatus> podStatusLookup = new();

        async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //initially load all the pods and their status's
                var initialResourceVersion = await InitialLoadAsync(cancellationToken);

                // We start the watch from the resource version we initially loaded.
                // This means we only receive events that occur after the resource version
                await WatchAllPods(initialResourceVersion, OnNewEvent, ex =>
                    {
                        log.Error(ex, "An unhandled error occured in monitoring the pods");
                    }, cancellationToken
                );
            }
        }
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

        internal async Task<string> InitialLoadAsync(CancellationToken cancellationToken)
        {
            log.Verbose("Preloading pod statuses");
            //clear the status'
            podStatusLookup.Clear();

            var allPods = await ListAllPodsAsync(cancellationToken);
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
        
        public async Task<V1PodList> ListAllPodsAsync(CancellationToken cancellationToken)
        {
            return await client.ListNamespacedPodAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }
        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            try
            {
                using var response = client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                    KubernetesConfig.Namespace,
                    labelSelector: OctopusLabels.ScriptTicketId,
                    resourceVersion: initialResourceVersion,
                    watch: true,
                    timeoutSeconds: 30,
                    cancellationToken: cancellationToken);

                var watchErrorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Action<Exception> internalOnError = ex =>
                {
                    //We cancel the watch explicitly (so it can be restarted)
                    watchErrorCancellationTokenSource.Cancel();

                    //notify there was an error
                    onError(ex);
                };

                await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
                {
                    await onChange(type, pod);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Foo");
            }
        }
    }
}