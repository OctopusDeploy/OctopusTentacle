using FluentAssertions;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class KubernetesAgentMetricsIntegrationTest : KubernetesAgentIntegrationTest
{
    readonly ISystemLog systemLog = new SystemLog();
    
    [Test]
    public void FetchingTimestampFromEmptyConfigMapEntryShouldBeMinValue()
    {
        //Arrange
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, "alternate-agent-metrics",new MapFromConfigMapToEventList(), systemLog);

        //Act
        var result = metrics.GetLatestEventTimestamp();
        
        //Assert
        result.Should().Be(DateTimeOffset.MinValue);
    }

    [Test]
    public void FetchingLatestEventTimestampFromNonexistentConfigMapThrowsException()
    {
        //Arrange
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, "agent-metrics",new MapFromConfigMapToEventList(), systemLog);

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
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, "agent-metrics",new MapFromConfigMapToEventList(), systemLog);

        //Act
        Action act = () => metrics.TrackEvent(new EventRecord("reason", "source", DateTimeOffset.Now));

        //Assert
        act.Should().NotThrow();
    }

    [Test]
    public void WritingEventToExistingConfigMapShouldPersistJsonEntry()
    {
        //Arrange
        var entryKey = "alternate-agent-metrics";
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, entryKey,new MapFromConfigMapToEventList(), systemLog);

        var @event = new EventRecord("reason", "source", DateTimeOffset.Now);
        
        //Act
        metrics.TrackEvent(@event);
        
        //Assert
        var jsonBody = persistenceProvider.GetValue(entryKey);
        var persistedData = JsonConvert.DeserializeObject<List<EventRecord>>(jsonBody);
        persistedData.Should().ContainSingle(er => er.Equals(@event));
    }
}