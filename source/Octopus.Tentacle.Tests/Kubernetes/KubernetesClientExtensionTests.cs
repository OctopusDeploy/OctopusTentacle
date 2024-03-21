using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class KubernetesClientExtensionTests
    {
        [Test]
        public async Task GetLogsSinceTime()
        {
            var sut = new k8s.Kubernetes(new LocalMachineKubernetesClientConfigProvider().Get());


            var logStream = await sut.GetNamespacedPodLogsAsync("octopus-agent-tentacle-85497f5679-f9fml", "octopus-agent-minikubeagent", "octopus-agent-tentacle", DateTimeOffset.Parse("2024-03-19T00:49:16.5408Z"), CancellationToken.None);

            using var streamReader = new StreamReader(logStream);
            var foo = await streamReader.ReadToEndAsync();
        }
    }
}