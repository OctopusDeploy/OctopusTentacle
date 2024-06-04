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
            sut.TrackEvent("Killed", "NFS Pod", eventTimestamp, 5);

            //Assert
            var typedResult = persistenceProvider.ReadValues().ToDictionary(
                pair => pair.Key,
                pair => JsonConvert.DeserializeObject<SourceEventCounts>(pair.Value));

            typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<CountSinceEpoch>>>
            {
                { "Killed", new Dictionary<string, List<CountSinceEpoch>> { { "NFS Pod", new List<CountSinceEpoch> { new CountSinceEpoch(eventTimestamp, 5) } } } }
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
            sut.TrackEvent("Killed", "NFS Pod", eventTimestamp, 1);
            sut.TrackEvent("Created", "NFS Pod", eventTimestamp, 1);
            sut.TrackEvent("Created", "Script Pod", eventTimestamp, 1);
            sut.TrackEvent("Restarted", "Script Pod", eventTimestamp, 1);
            
            //Assert
            var typedResult = persistenceProvider.ReadValues().ToDictionary(
                pair => pair.Key,
                pair => JsonConvert.DeserializeObject<SourceEventCounts>(pair.Value));

            typedResult.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<CountSinceEpoch>>>
            {
                { "Killed", new Dictionary<string, List<CountSinceEpoch>> {{ "NFS Pod", new List<CountSinceEpoch> { new(eventTimestamp, 1) } }}},
                { "Created", new Dictionary<string, List<CountSinceEpoch>>
                {
                    { "NFS Pod", new List<CountSinceEpoch> { new(eventTimestamp, 1) } },
                    { "Script Pod", new List<CountSinceEpoch> { new(eventTimestamp, 1) } }
                }},
                { "Restarted", new Dictionary<string, List<CountSinceEpoch>> {{ "Script Pod", new List<CountSinceEpoch> { new(eventTimestamp, 1) } }}},
            });
        }

        [Test]
        public void TrackEventDoesNotPropagateExceptions()
        {
            IPersistenceProvider persistenceProvider = Substitute.For<IPersistenceProvider>();
            persistenceProvider.GetValue(Arg.Any<string>()).Throws(new Exception("Something broke"));
            var sut = new KubernetesAgentMetrics(persistenceProvider, systemLog);

            Action act = () => sut.TrackEvent("Killed", "NFS Pod", DateTimeOffset.Now, 1);

            act.Should().NotThrow();
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