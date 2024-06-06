extern alias TaskScheduler;
using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Diagnostics;

namespace Octopus.Tentacle.Tests.Kubernetes
{
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
            var sut = new KubernetesEventMonitor(agentMetrics, eventService);

            await sut.CacheNewEvents(tokenSource.Token);

            agentMetrics.DidNotReceiveWithAnyArgs().TrackEvent(default!, default!, default);

        }

        [Test]
        public async Task NfsPodStartAndKillingEventsAreTrackedInMetrics()
        {
            var agentMetrics = Substitute.For<IKubernetesAgentMetrics>();
            agentMetrics.GetLatestEventTimestamp().ReturnsForAnyArgs(testEpoch);
            var eventService = Substitute.For<IKubernetesEventService>();
            eventSe5rvice.
            
            
            
            await Task.CompletedTask;
        }

        [Test]
        public async Task NfsWatchDogEventsAreTrackedInMetrics()
        {
            await Task.CompletedTask;
        }

        [Test]
        public async Task EventsOlderThanMetricsTimestampCursorAreNotAddedToMetrics()
        {
            await Task.CompletedTask;
        }
    }
}