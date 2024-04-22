using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class Foo
    {
        [Test]
        public async Task fdfs()
        {
            var client = new k8s.Kubernetes(new LocalMachineKubernetesClientConfigProvider().Get());

            while (true)
            {
                Console.WriteLine("Loop");

                var foo = await client.CoreV1.ListNamespacedEventAsync("octopus-agent-nfsbig", fieldSelector: "type!=Normal");

                var events = foo.Items;
                
                
                var pods = await client.ListNamespacedPodAsync("octopus-agent-nfsbig");
                var v1Pods = pods.Items.Where(p => p.Name().StartsWith("octopus-agent-tentacle-") && p.Status.Phase == "Running").OrderByDescending(p => p.Status.StartTime).ToList();

                if (v1Pods.Count > 1)
                {
                    Console.WriteLine("Scream");
                    foreach (var pod in v1Pods)
                    {
                        Console.WriteLine($"Pods: {pod.Name()}, Status: {pod.Status.Phase}, StartTime: {pod.Status.StartTime}");
                    }
                }

                var v1Pod = v1Pods.First();
                await client.DeleteNamespacedPodAsync(v1Pod.Name(), v1Pod.Namespace());

                await Task.Delay(20000);
            }
        }
    }
}