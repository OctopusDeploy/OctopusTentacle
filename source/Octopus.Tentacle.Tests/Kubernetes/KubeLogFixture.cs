using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class KubeLogFixture
    {
        [Test]
        public async Task GetLogsSinceTime()
        {
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE", "octopus-agent-aksadmin");

            var client = new k8s.Kubernetes(new LocalMachineKubernetesClientConfigProvider().Get());
            var allPods = (await client.ListPodForAllNamespacesAsync()).Items.Where(p => p.Status.Phase == "Running").ToList();

            for (int i = 0; i < 7; i++)
                allPods = allPods.Concat(allPods).ToList();

            
            allPods.ForEach(async p => await GetLogs(p.Name(), p.Namespace(), p.Spec.Containers.First().Name));

            
            // var tasks = allPods.Select(p => 
            //     GetLogs(p.Name(), p.Namespace(), p.Spec.Containers.First().Name));
            //
            // //var tasks = Enumerable.Range(1, 10000).Select(_ => GetLogs("octopus-agent-tentacle-85b6784797-c6mlb", "octopus-agent-nfsbig", "octopus-agent-tentacle"));
            //
            // await Task.WhenAll(tasks);

            // await Parallel.ForEachAsync(Enumerable.Range(1, 1000), async (_, ct) =>
            // {
            //     await GetLogs();
            // });
        }

        static async Task GetLogs(string podName, string nameSpace, string containerName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var client = new k8s.Kubernetes(new LocalMachineKubernetesClientConfigProvider().Get());
            await using var stream = await client.GetNamespacedPodLogsAsync(podName, nameSpace, containerName);

            using var reader = new StreamReader(stream);
            var firstLine = await reader.ReadLineAsync();

            await File.AppendAllLinesAsync("/Users/franklin/timings.txt", new[] { $"{DateTime.UtcNow:O} Pod {podName} took {stopwatch.ElapsedMilliseconds}ms" });
        }
    }
}