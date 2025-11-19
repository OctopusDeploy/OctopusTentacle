using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Tests.Diagnostics.Metrics
{
    public class KubernetesAgentMetricsFixture
    {
        readonly ISystemLog systemLog = Substitute.For<ISystemLog>();

        [Test]
        public async Task CanAddMetricToAnEmptyPersistenceMap()
        {
            //Arrange
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            //Act
            var eventTimestamp = DateTimeOffset.Now;
            await sut.TrackEvent("Killed", "NFS Pod", eventTimestamp, CancellationToken.None);

            //Assert
            var persistedDictionary = await persistenceProvider.ReadValues(CancellationToken.None);
            var metricsData = persistedDictionary["metrics"];
            var typedResult = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<DateTimeOffset>>>>(metricsData);

            typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "Killed", new Dictionary<string, List<DateTimeOffset>> { { "NFS Pod", new List<DateTimeOffset> { eventTimestamp } } } }
            });
        }

        [Test]
        public async Task TrackingMultipleActionsAndSourcesResultsInAFullEventList()
        {
            //Arrange
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            //Act
            var eventTimestamp = DateTimeOffset.Now;
            await sut.TrackEvent("Killed", "NFS Pod", eventTimestamp, CancellationToken.None);
            await sut.TrackEvent("Created", "NFS Pod", eventTimestamp, CancellationToken.None);
            await sut.TrackEvent("Created", "Script Pod", eventTimestamp, CancellationToken.None);
            await  sut.TrackEvent("Restarted", "Script Pod", eventTimestamp, CancellationToken.None);
            
            //Assert
            var persistedDictionary = await persistenceProvider.ReadValues(CancellationToken.None);
            var metricsData = persistedDictionary["metrics"];
            var typedResult = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<DateTimeOffset>>>>(metricsData);

            typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "Killed", new Dictionary<string, List<DateTimeOffset>> {{ "NFS Pod", new List<DateTimeOffset> { eventTimestamp } }}},
                { "Created", new Dictionary<string, List<DateTimeOffset>>
                {
                    { "NFS Pod", new List<DateTimeOffset> { eventTimestamp } },
                    { "Script Pod", new List<DateTimeOffset> { eventTimestamp } }
                }},
                { "Restarted", new Dictionary<string, List<DateTimeOffset>> {{ "Script Pod", new List<DateTimeOffset> { eventTimestamp } }}},
            });
        }

        [Test]
        public async Task TrackEventDoesNotPropagateExceptions()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>(), Arg.Any<CancellationToken>()).Throws(new Exception("Something broke"));
            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            Func<Task> func = async () => await sut.TrackEvent("Killed", "NFS Pod", DateTimeOffset.Now, CancellationToken.None);

            await func.Should().NotThrowAsync();
        }

        [Test]
        public async Task GetLatestTimestampReturnsDateTimeOffsetMinimumIfNoEventsExist()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            var result = await sut.GetLatestEventTimestamp(CancellationToken.None);

            result.Should().Be(DateTimeOffset.MinValue);
        }

        [Test]
        public async Task GetLatestTimestampReturnsTheChronologicallyLatestTimeNotNewestInList()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            var epoch = DateTimeOffset.Now;
            await sut.TrackEvent("Created", "NFS Pod", epoch, CancellationToken.None);
            await sut.TrackEvent("Killed", "NFS Pod", epoch.AddMinutes(1), CancellationToken.None);
            await sut.TrackEvent("Created", "Script Pod", epoch, CancellationToken.None);
            await sut.TrackEvent("Killed", "Script Pod", epoch.AddMinutes(-1), CancellationToken.None);

            var timeDate = await sut.GetLatestEventTimestamp(CancellationToken.None);

            timeDate.Should().BeExactly(epoch.AddMinutes(1));
        }

        [Test]
        public async Task GetLatestTimeStampPropagatesExceptionsIfUnderlyingPersistenceFails()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>(), Arg.Any<CancellationToken>()).Throws(new Exception("Something broke"));

            var sut = new KubernetesAgentMetrics(persistenceProvider, "metrics", systemLog);

            Func<Task> func = async () => await sut.GetLatestEventTimestamp(CancellationToken.None);

            await func.Should().ThrowAsync<Exception>().WithMessage("Something broke");
        }
    }

    public class MockPersistenceProvider : IPersistenceProvider
    {
        readonly Dictionary<string, string> content = new();

        public async Task<string?> GetValue(string key, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return content.TryGetValue(key, out var value) ? value : null;
        }

        public async Task PersistValue(string key, string value, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            content[key] = value;
        }

        public async Task<ImmutableDictionary<string, string>> ReadValues(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return content.ToImmutableDictionary();
        }
    }
}