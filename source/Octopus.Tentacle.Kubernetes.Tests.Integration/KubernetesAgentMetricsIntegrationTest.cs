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
    //[Ignore("Requires rework of configmap service before it can be used here")]
    public void FetchingTimestampFromEmptyConfigMapEntryShouldBeMinValue()
    {
        //Arrange
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, systemLog, kubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, systemLog);

        //Act
        var result = metrics.GetLatestEventTimestamp();
        
        //Assert
        result.Should().Be(DateTimeOffset.MinValue);
    }

    [Test]
    public void FetchingLatestEventTimestampFromNonexistentConfigMapThrowsException()
    {
        //Arrange
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, systemLog, kubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, systemLog);

        //Act
        Action act = () => metrics.GetLatestEventTimestamp();
        
        //Assert
        act.Should().Throw<Exception>();
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
        Action act = () => metrics.TrackEvent("reason", "source", DateTimeOffset.Now);

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
        metrics.TrackEvent("reason", "source", eventTimestamp);
        
        //Assert
        var persistedDictionary = persistenceProvider.ReadValues();
        var dataFields = persistedDictionary.Where(pair => pair.Key != "latestTimestamp");
        var typedResult = dataFields.ToDictionary(
            pair => pair.Key,
            pair => JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>(pair.Value));

        typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
        {
            { "reason", new Dictionary<string, List<DateTimeOffset>> { { "source", new List<DateTimeOffset> { eventTimestamp } } } }
        });
    }
}