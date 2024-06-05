using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Tests.Diagnostics.Metrics
{
    
    
    public class KubernetesAgentMetricsFixture
    {
        readonly ISystemLog systemLog = Substitute.For<ISystemLog>();

        [Test]
        public void CanAddMetricToAnEmptyPersistenceMap()
        {
            //Arrange
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            //Act
            var eventTimestamp = DateTimeOffset.Now;
            sut.TrackEvent("Killed", "NFS Pod", eventTimestamp);

            //Assert
            var persistedDictionary = persistenceProvider.ReadValues();
            var dataFields = persistedDictionary.Where(pair => pair.Key != "latestTimestamp");
            var typedResult = dataFields.ToDictionary(
                pair => pair.Key,
                pair => JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>(pair.Value));

            typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "Killed", new Dictionary<string, List<DateTimeOffset>> { { "NFS Pod", new List<DateTimeOffset> { eventTimestamp } } } }
            });
        }

        [Test]
        public void TrackingMultipleActionsAndSourcesResultsInAFullEventList()
        {
            //Arrange
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            //Act
            var eventTimestamp = DateTimeOffset.Now;
            sut.TrackEvent("Killed", "NFS Pod", eventTimestamp);
            sut.TrackEvent("Created", "NFS Pod", eventTimestamp);
            sut.TrackEvent("Created", "Script Pod", eventTimestamp);
            sut.TrackEvent("Restarted", "Script Pod", eventTimestamp);
            
            //Assert
            var persistedDictionary = persistenceProvider.ReadValues();
            var dataFields = persistedDictionary.Where(pair => pair.Key != "latestTimestamp");
            var typedResult = dataFields.ToDictionary(
                pair => pair.Key,
                pair => JsonConvert.DeserializeObject<Dictionary<string, List<DateTimeOffset>>>(pair.Value));

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
        public void TrackEventDoesNotPropagateExceptions()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>()).Throws(new Exception("Something broke"));
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            Action act = () => sut.TrackEvent("Killed", "NFS Pod", DateTimeOffset.Now);

            act.Should().NotThrow();
        }

        [Test]
        public void GetLatestTimestampReturnsDateTimeOffsetMinimumIfNoEventsExist()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            sut.GetLatestEventTimestamp().Should().Be(DateTimeOffset.MinValue);
        }

        [Test]
        public void GetLatestTimestampReturnsTheChronologicallyLatestTimeNotNewestInList()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            var epoch = DateTimeOffset.Now;
            sut.TrackEvent("Created", "NFS Pod", epoch);
            sut.TrackEvent("Killed", "NFS Pod", epoch.AddMinutes(1));
            sut.TrackEvent("Created", "Script Pod", epoch);
            sut.TrackEvent("Killed", "Script Pod", epoch.AddMinutes(-1));

            var timeDate = sut.GetLatestEventTimestamp();

            timeDate.Should().BeExactly(epoch.AddMinutes(1));
        }

        [Test]
        public void GetLatestTimeStampPropagatesExceptionsIfUnderlyingPersistenceFails()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>()).Throws(new Exception("Something broke"));

            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            Action act = () => sut.GetLatestEventTimestamp();

            act.Should().Throw<Exception>().WithMessage("Something broke");
        }
    }

    public class MockPersistenceProvider : IPersistenceProvider
    {
        Dictionary<string, string> Content = new();

        public string GetValue(string key)
        {
            return Content.TryGetValue(key, out var value) ? value : "";
        }

        public void PersistValue(string key, string value)
        {
            Content[key] = value;
        }

        public ImmutableDictionary<string, string> ReadValues()
        {
            return Content.ToImmutableDictionary();
        }
    }
}