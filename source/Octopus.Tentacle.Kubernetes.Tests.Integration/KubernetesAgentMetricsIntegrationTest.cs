using System;
using FluentAssertions;
using k8s;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class KubernetesAgentMetricsIntegrationTest : KubernetesAgentIntegrationTest
{
    readonly ISystemLog systemLog = new SystemLog();

    class KubernetesFileWrappedProvider : IKubernetesClientConfigProvider
    {
        readonly string filename;

        public KubernetesFileWrappedProvider(string filename)
        {
            this.filename = filename;
        }

        public KubernetesClientConfiguration Get()
        {
            return KubernetesClientConfiguration.BuildConfigFromConfigFile(filename);
        }
    }

    [Test]
    public void WritingEventToNonExistentConfigMapShouldFailSilently()
    {
        //Arrange
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, systemLog, kubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, systemLog);

        //Act
        Action act = () => metrics.TrackEvent("reason", "source", DateTimeOffset.Now, 1);

        //Assert
        act.Should().NotThrow();
    }

    [Test]
    public void WritingEventToExistingConfigMapShouldPersistJsonEntry()
    {
        //Arrange
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, systemLog, kubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, systemLog);

        //Act
        var eventTimestamp = DateTimeOffset.Now;
        metrics.TrackEvent("reason", "source", eventTimestamp, 1);
        
        //Assert
        var typedResult = persistenceProvider.ReadValues().ToDictionary(
            pair => pair.Key,
            pair => JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>(pair.Value));

        typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
        {
            { "reason", new Dictionary<string, List<DateTimeOffset>> { { "source", new List<DateTimeOffset> { eventTimestamp } } } }
        });
    }
}