using System;
using FluentAssertions;
using k8s;
using Newtonsoft.Json;
using NSubstitute;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[HelmVersion1TestFixture]
[HelmVersion2AlphaTestFixture]
public class KubernetesAgentMetricsIntegrationTest : KubernetesAgentIntegrationTest
{
    readonly ISystemLog systemLog = new SystemLog();


    public KubernetesAgentMetricsIntegrationTest(string? helmChartVersion) 
        : base(helmChartVersion)
    { }
    
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
    public async Task FetchingTimestampFromEmptyConfigMapEntryShouldBeMinValue()
    {
        //Arrange
        var config = Substitute.For<IKubernetesConfiguration>();
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, config, systemLog, KubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", config, configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, ConfigMapNames.AgentMetricsConfigMapKey, systemLog);

        //Act
        var result = await metrics.GetLatestEventTimestamp(CancellationToken.None);

        //Assert
        result.Should().Be(DateTimeOffset.MinValue);
    }

    [Test]
    public async Task FetchingLatestEventTimestampFromNonexistentConfigMapThrowsException()
    {
        //Arrange
        var config = Substitute.For<IKubernetesConfiguration>();
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, config, systemLog, KubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", config, configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

        //Act
        Func<Task> func = async () => await metrics.GetLatestEventTimestamp(CancellationToken.None);

        //Assert
        await func.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task WritingEventToNonExistentConfigMapShouldFailSilently()
    {
        //Arrange
        var config = Substitute.For<IKubernetesConfiguration>();
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, config, systemLog, KubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("nonexistent-config-map", config, configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, ConfigMapNames.AgentMetricsConfigMapKey, systemLog);

        //Act
        var func = async () => await metrics.TrackEvent("reason", "source", DateTimeOffset.Now, CancellationToken.None);

        //Assert
        await func.Should().NotThrowAsync();
    }

    [Test]
    public async Task WritingEventToExistingConfigMapShouldPersistJsonEntry()
    {
        //Arrange
        var config = Substitute.For<IKubernetesConfiguration>();
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var configMapService = new Support.TestSupportConfigMapService(kubernetesConfigClient, config, systemLog, KubernetesAgentInstaller.Namespace);
        var persistenceProvider = new PersistenceProvider("kubernetes-agent-metrics", config, configMapService);
        var metrics = new KubernetesAgentMetrics(persistenceProvider, ConfigMapNames.AgentMetricsConfigMapKey, systemLog);

        //Act
        var eventTimestamp = DateTimeOffset.Now;
        await metrics.TrackEvent("reason", "source", eventTimestamp, CancellationToken.None);

        //Assert
        var persistedDictionary = await persistenceProvider.ReadValues(CancellationToken.None);
        var metricsData = persistedDictionary[ConfigMapNames.AgentMetricsConfigMapKey];
        var typedResult = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<DateTimeOffset>>>>(metricsData);

        typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
        {
            { "reason", new Dictionary<string, List<DateTimeOffset>> { { "source", new List<DateTimeOffset> { eventTimestamp } } } }
        });
    }
}