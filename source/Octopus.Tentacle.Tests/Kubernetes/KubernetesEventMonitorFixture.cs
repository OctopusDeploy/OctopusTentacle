extern alias TaskScheduler;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using k8s.Models;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class StubbedAgentMetrics : IKubernetesAgentMetrics
    {
        readonly DateTimeOffset latestTimeStamp;
        public Dictionary<string, Dictionary<string, List<DateTimeOffset>>> Events { get; } = new();

        public StubbedAgentMetrics(DateTimeOffset latestTimeStamp)
        {
            this.latestTimeStamp = latestTimeStamp;
        }

        public void TrackEvent(string reason, string source, DateTimeOffset occurrence)
        {
            if (!Events.ContainsKey(reason))
            {
                Events.Add(reason, new Dictionary<string, List<DateTimeOffset>>());
            }

            if (!Events[reason].ContainsKey(source))
            {
                Events[reason].Add(source, new List<DateTimeOffset>());
            }

            Events[reason][source].Add(occurrence);
        }

        public DateTimeOffset GetLatestEventTimestamp()
        {
            return latestTimeStamp;
        }
    }

    public class KubernetesEventMonitorFixture
    {
        readonly CancellationTokenSource tokenSource = new();
        readonly DateTimeOffset testEpoch = DateTimeOffset.Now;

        [Test]
        public async Task NoEntriesAreSentToMetricsWhenEventListIsEmpty()
        {
            var agentMetrics = Substitute.For<IKubernetesAgentMetrics>();
            agentMetrics.GetLatestEventTimestamp().ReturnsForAnyArgs(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            var sut = new KubernetesEventMonitor(agentMetrics, eventService, "arbitraryNamespace", new IEventMapper[]{new NfsPodRestarted(), new AgentKilledEventMapper(), new NfsStaleEventMapper()});

            await sut.CacheNewEvents(tokenSource.Token);

            agentMetrics.DidNotReceiveWithAnyArgs().TrackEvent(default!, default!, default);
        }

        [Test]
        public async Task NfsPodStartAndKillingEventsAreTrackedInMetrics()
        {
            //Arrange
            var agentMetrics = new StubbedAgentMetrics(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            eventService.FetchAllEventsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
                new Corev1EventList(new List<Corev1Event>
                {
                    new()
                    {
                        Reason = "Started",
                        Metadata = new V1ObjectMeta()
                        {
                            Name = "octopus-agent-nfs",
                        },
                        FirstTimestamp = testEpoch.DateTime.AddSeconds(1),
                        LastTimestamp = testEpoch.DateTime.AddSeconds(1)
                    },
                    new()
                    {
                        Reason = "Killing",
                        Metadata = new V1ObjectMeta()
                        {
                            Name = "octopus-agent-nfs",
                        },
                        FirstTimestamp = testEpoch.DateTime.AddMinutes(1),
                        LastTimestamp = testEpoch.DateTime.AddMinutes(1)
                    }
                }));
            var sut = new KubernetesEventMonitor(agentMetrics, eventService, "arbitraryNamespace", new IEventMapper[]{new NfsPodRestarted(), new AgentKilledEventMapper(), new NfsStaleEventMapper()});

            //Act
            await sut.CacheNewEvents(tokenSource.Token);

            //Assert
            agentMetrics.Events.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "Started", new Dictionary<string, List<DateTimeOffset>> { { "octopus-agent-nfs", new List<DateTimeOffset>() { testEpoch.AddSeconds(1) } } } },
                { "Killing", new Dictionary<string, List<DateTimeOffset>> { { "octopus-agent-nfs", new List<DateTimeOffset>() { testEpoch.AddMinutes(1) } } } },
            });
        }

        [Test]
        public async Task NfsWatchDogEventsAreTrackedInMetrics()
        {
            //Arrange
            var podName = "octopus-script-123412341234.123412341234";
            var agentMetrics = new StubbedAgentMetrics(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            eventService.FetchAllEventsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
                new Corev1EventList(new List<Corev1Event>
                {
                    new()
                    {
                        Reason = "NfsWatchdogTimeout",
                        Metadata = new V1ObjectMeta()
                        {
                            Name = podName,
                        },
                        FirstTimestamp = testEpoch.DateTime.AddSeconds(1),
                        LastTimestamp = testEpoch.DateTime.AddSeconds(1)
                    }
                }));

            var sut = new KubernetesEventMonitor(agentMetrics, eventService, "arbitraryNamespace", new IEventMapper[]{new NfsPodRestarted(), new AgentKilledEventMapper(), new NfsStaleEventMapper()});

            //Act
            await sut.CacheNewEvents(tokenSource.Token);

            //Assert
            agentMetrics.Events.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "NfsWatchdogTimeout", new Dictionary<string, List<DateTimeOffset>> { { podName, new List<DateTimeOffset>() { testEpoch.AddSeconds(1) } } } },
            });
        }

        [Test]
        public async Task EventsOlderThanOrEqualToMetricsTimestampCursorAreNotAddedToMetrics()
        {
            //Arrange
            var podName = "octopus-script-123412341234.123412341234";
            var agentMetrics = new StubbedAgentMetrics(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            eventService.FetchAllEventsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
                new Corev1EventList(new List<Corev1Event>
                {
                    new()
                    {
                        Reason = "NfsWatchdogTimeout",
                        Metadata = new V1ObjectMeta()
                        {
                            Name = podName,
                        },
                        FirstTimestamp = testEpoch.DateTime,
                        LastTimestamp = testEpoch.DateTime
                    }
                }));
            
            var sut = new KubernetesEventMonitor(agentMetrics, eventService, "arbitraryNamespace", new IEventMapper[]{new NfsPodRestarted(), new AgentKilledEventMapper(), new NfsStaleEventMapper()});
            //Act
            await sut.CacheNewEvents(tokenSource.Token);
            
            //Assert
            agentMetrics.Events.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>());
        }

        [Test]
        public async Task NewestTimeStampInEventIsUsedToDetermineAgeAndAsMetricsValue()
        {
            //Arrange
            var podName = "octopus-script-123412341234.123412341234";
            var agentMetrics = new StubbedAgentMetrics(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            eventService.FetchAllEventsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
                new Corev1EventList(new List<Corev1Event>
                {
                    new()
                    {
                        Reason = "NfsWatchdogTimeout",
                        Metadata = new V1ObjectMeta()
                        {
                            Name = podName,
                        },
                        FirstTimestamp = testEpoch.DateTime.AddMinutes(-2),
                        LastTimestamp = testEpoch.DateTime.AddMinutes(-1),
                        EventTime = testEpoch.DateTime.AddMinutes(1)
                    }
                }));
            
            var sut = new KubernetesEventMonitor(agentMetrics, eventService, "arbitraryNamespace", new IEventMapper[]{new NfsPodRestarted(), new AgentKilledEventMapper(), new NfsStaleEventMapper()});
            //Act
            await sut.CacheNewEvents(tokenSource.Token);
            
            //Assert
            // The event.EventTime is newest event Time stamp, and is larger than the last metric date (TestEpoch) as such
            // the event should factored into the metrics, and should report this latest time value.
            agentMetrics.Events.Should().BeEquivalentTo(new Dictionary<string, Dictionary<string, List<DateTimeOffset>>>
            {
                { "NfsWatchdogTimeout", new Dictionary<string, List<DateTimeOffset>> { { podName, new List<DateTimeOffset>() { testEpoch.AddMinutes(1) } } } },
            });
        }
    }
}