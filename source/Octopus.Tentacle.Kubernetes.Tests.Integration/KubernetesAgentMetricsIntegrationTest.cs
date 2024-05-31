using FluentAssertions;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Diagnostics.Metrics;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class KubernetesAgentMetricsIntegrationTest : KubernetesAgentIntegrationTest
{

    ISystemLog systemLog = new SystemLog();
    
    //TODO: This maybe flakey - if an NFS failure is detected, the map won't be empty!
    [Test]
    public void FetchingTimestampFromEmptyConfigMapShouldBeMinValue()
    {
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);

        var result = metrics.GetLatestEventTimestamp();
        result.Should().Be(DateTimeOffset.MinValue);
    }

    [Test]
    public void FetchingLatestEventTimestampFromNonexistentConfigMapThrowsException()
    {
        var kubernetesConfigClient = new InClusterKubernetesClientConfigProvider();
        var configMapService = new KubernetesConfigMapService(kubernetesConfigClient, systemLog);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", configMapService, systemLog);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);

        Action act = () => metrics.GetLatestEventTimestamp();

        act.Should().Throw<Exception>();
    }

    [Test]
    public void WritingEventToNonExistentConfigMapShouldFailSilently()
    {
        
    }

    [Test]
    public void WritingEventToExistingConfigMapShouldPersistJsonEntry()
    {
        
    }
}