using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.Core.Arguments;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics.Metrics;

namespace Octopus.Tentacle.Tests.Diagnostics.Metrics
{
    public class KubernetesAgentMetricsFixture
    {
        readonly ISystemLog systemLog = Substitute.For<ISystemLog>();
        
        [Test]
        public void CanAddMetricToAnEmptyPersistenceMap()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);
            
            var @event = new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now);
            sut.TrackEvent(@event);

            persistenceProvider.Content.Keys.Should().ContainSingle(entry => entry.Equals(KubernetesAgentMetrics.EntryName));
            var items = JsonConvert.DeserializeObject<List<EventRecord>>(persistenceProvider.Content[KubernetesAgentMetrics.EntryName]);
            items.Count.Should().Be(1);
            items.First().Should().BeEquivalentTo(@event);
        }

        [Test]
        public void CanWriteMultipleEntriesToThePersistenceMap()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);

            var events = new List<EventRecord>()
            {
                new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now),
                new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now.AddMinutes(1))
            };
            
            events.ForEach(e => sut.TrackEvent(e));

            persistenceProvider.Content.Keys.Should().ContainSingle(entry => entry.Equals(KubernetesAgentMetrics.EntryName));
            var items = JsonConvert.DeserializeObject<List<EventRecord>>(persistenceProvider.Content[KubernetesAgentMetrics.EntryName]);
            items.Count.Should().Be(2);
            items.Should().BeEquivalentTo(events);
        }

        [Test]
        public void TrackEventDoesNotPropagateExceptions()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>()).Throws(new Exception("Something broke"));
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);
            
            var @event = new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now);
            Action act = () => sut.TrackEvent(@event);

            act.Should().NotThrow();
        }

        [Test]
        public void GetLatestTimestampReturnsDateTimeOffsetMinimumIfNoEventsExist()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);

            sut.GetLatestEventTimestamp().Should().Be(DateTimeOffset.MinValue);
        }

        [Test]
        public void GetLatestTimestampReturnsTheChronologicallyLatestTimeNotNewestInList()
        {
            MockPersistenceProvider persistenceProvider = new();
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);
            
            var events = new List<EventRecord>()
            {
                new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now),
                new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now.AddMinutes(1)),
                new EventRecord("Killed", "NFS Pod", DateTimeOffset.Now.AddMinutes(-1))
            };

            events.ForEach(e => sut.TrackEvent(e));

            var timeDate = sut.GetLatestEventTimestamp();

            timeDate.Should().BeExactly(events[1].Timestamp);
        }

        [Test]
        public void GetLatestTimeStampPropagatesExceptionsIfUnderlyingPersistenceFails()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>()).Throws(new Exception("Something broke"));
            
            var sut = new KubernetesAgentMetrics(persistenceProvider, new MapFromConfigMapToEventList(), systemLog);
            
            Action act = () => sut.GetLatestEventTimestamp();

            act.Should().Throw<Exception>().WithMessage("Something broke");
        }
        
    }
    
    public class MockPersistenceProvider : IPersistenceProvider
    {
        public Dictionary<string, string> Content = new();
        public string GetValue(string key)
        {
            return Content.TryGetValue(key, out var value) ? value : "";
        }

        public void PersistValue(string key, string value)
        {
            Content[key] = value;
        }
    }
}