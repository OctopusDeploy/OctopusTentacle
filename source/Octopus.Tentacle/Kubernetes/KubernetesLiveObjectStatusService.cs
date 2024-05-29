using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Time;
using Octopus.Tentacle.Util;
using Polly;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesLiveObjectStatusService : KubernetesService
    {
        readonly ISystemLog log;
        readonly HalibutRuntime halibut;
        readonly HalibutEndpointDiscovery endpointDiscovery;
        
        public KubernetesLiveObjectStatusService(IKubernetesClientConfigProvider configProvider, ISystemLog log, HalibutRuntime halibut, HalibutEndpointDiscovery endpointDiscovery)
            : base(configProvider, log)
        {
            this.log = log;
            this.halibut = halibut;
            this.endpointDiscovery = endpointDiscovery;
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
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
            var c = halibut.CreateAsyncClient<IMyEchoService, IAsyncClientMyEchoService>(endpointDiscovery.GetPollingEndpoints().First());

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var @namespace in LobsterResources.NamespacesToMonitor())
                {
                    var pods = await Client.ListNamespacedPodAsync(@namespace, cancellationToken: cancellationToken);
                
                    foreach (var pod in pods)
                    {
                        var response = await c.SayHelloAsync($"Pod {pod.Namespace()}:{pod.Name()} - {pod.Status.Phase}");
                        log.Info("Message Back From Server:" + response);
                    }
                }                    
                
                
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }
    
    public static class LobsterResources
    {
        static readonly HashSet<string> namespaces = new HashSet<string>() { "octopus-agent-agentlobs" }; 
        public static string[] NamespacesToMonitor()
        {
            lock(namespaces)
                return namespaces.ToArray();
        }

        public static void UpdateNamespaces(string[] namespaces)
        {
            lock(namespaces)
                namespaces.AddRange(namespaces);
        }
    }
}