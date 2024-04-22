using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Tests.Support;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class KubernetesOrphanedPodCleanerTests
    {
        IKubernetesPodService podService;
        InMemoryLog log;
        FixedClock clock;
        IKubernetesPodStatusProvider monitor;
        ScriptTicket scriptTicket;
        KubernetesOrphanedPodCleaner cleaner;
        TimeSpan overCutoff;
        TimeSpan underCutoff;
        DateTimeOffset startTime;
        ITentacleScriptLogProvider scriptLogProvider;
        IScriptPodSinceTimeStore scriptPodSinceTimeStore;

        [SetUp]
        public void Setup()
        {
            startTime = DateTimeOffset.MinValue.ToUniversalTime() + 1.Days();
            podService = Substitute.For<IKubernetesPodService>();
            
            log = new InMemoryLog();
            clock = new FixedClock(startTime);
            scriptLogProvider = Substitute.For<ITentacleScriptLogProvider>();
            scriptPodSinceTimeStore = Substitute.For<IScriptPodSinceTimeStore>();
            monitor = Substitute.For<IKubernetesPodStatusProvider>();
            scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());

            cleaner = new KubernetesOrphanedPodCleaner(monitor, podService, log, clock, scriptLogProvider, scriptPodSinceTimeStore);

            overCutoff = cleaner.CompletedPodConsideredOrphanedAfterTimeSpan + 1.Minutes();
            underCutoff = cleaner.CompletedPodConsideredOrphanedAfterTimeSpan - 1.Minutes();
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__DISABLEAUTOPODCLEANUP", null);
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__PODSCONSIDEREDORPHANEDAFTERMINUTES", null);
        }

        [Test]
        public async Task OrphanedPodCleanedUpIfOver10MinutesHavePassed()
        {
            //Arrange
            var pods = new List<ITrackedScriptPod>
            {
                CreatePod(TrackedScriptPodState.Succeeded(0, startTime))
            };
            monitor.GetAllTrackedScriptPods().Returns(pods);
            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.Received().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
            scriptLogProvider.Received().Delete(scriptTicket);
            scriptPodSinceTimeStore.Received().Delete(scriptTicket);
        }

        [TestCase(TrackedScriptPodPhase.Succeeded, true)]
        [TestCase(TrackedScriptPodPhase.Failed, true)]
        [TestCase(TrackedScriptPodPhase.Running, false)]
        public async Task OrphanedPodOnlyCleanedUpWhenNotRunning(TrackedScriptPodPhase phase, bool shouldBeDeleted)
        {
            //Arrange
            var pods = new List<ITrackedScriptPod>()
            {
                CreatePod(CreateState(phase))
            };
            monitor.GetAllTrackedScriptPods().Returns(pods);
            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            if (shouldBeDeleted)
            {
                await podService.Received().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
                scriptLogProvider.Received().Delete(scriptTicket);
                scriptPodSinceTimeStore.Received().Delete(scriptTicket);
            }
            else
            {
                await podService.DidNotReceiveWithAnyArgs().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
                scriptLogProvider.DidNotReceiveWithAnyArgs().Delete(scriptTicket);
                scriptPodSinceTimeStore.DidNotReceiveWithAnyArgs().Delete(scriptTicket);
            }
            
            TrackedScriptPodState CreateState(TrackedScriptPodPhase phase)
            {
                switch (phase)
                {
                    case TrackedScriptPodPhase.Running:
                        return TrackedScriptPodState.Running();
                    case TrackedScriptPodPhase.Succeeded:
                        return TrackedScriptPodState.Succeeded(0, startTime);
                    case TrackedScriptPodPhase.Failed:
                        return TrackedScriptPodState.Failed(-1, startTime);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
                }
            }
        }

        [Test]
        public async Task OrphanedPodNotCleanedUpIfOnly9MinutesHavePassed()
        {
            //Arrange
            var pods = new List<ITrackedScriptPod>
            {
                CreatePod(TrackedScriptPodState.Succeeded(0, startTime))
            };
            monitor.GetAllTrackedScriptPods().Returns(pods);
            clock.WindForward(underCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.DidNotReceiveWithAnyArgs().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
            scriptLogProvider.DidNotReceiveWithAnyArgs().Delete(scriptTicket);
        }

        [Test]
        public async Task OrphanedPodNotCleanedUpIfPodCleanupIsDisabled()
        {
            //Arrange
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__DISABLEAUTOPODCLEANUP", "true");
            var pods = new List<ITrackedScriptPod>
            {
                CreatePod(TrackedScriptPodState.Succeeded(0, startTime))
            };
            monitor.GetAllTrackedScriptPods().Returns(pods);
            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.DidNotReceive().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
            scriptLogProvider.Received().Delete(scriptTicket);
            scriptPodSinceTimeStore.Received().Delete(scriptTicket);
        }

        [TestCase(1, false)]
        [TestCase(3, true)]
        public async Task EnvironmentVariableDictatesWhenPodsAreConsideredOrphaned(int checkAfterMinutes, bool shouldDelete)
        {
            //Arrange
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__PODSCONSIDEREDORPHANEDAFTERMINUTES", "2");

            // We need to reinitialise the sut after changing the env var value
            cleaner = new KubernetesOrphanedPodCleaner(monitor, podService, log, clock, scriptLogProvider, scriptPodSinceTimeStore);
            var pods = new List<ITrackedScriptPod>
            {
                CreatePod(TrackedScriptPodState.Succeeded(0, startTime))
            };
            monitor.GetAllTrackedScriptPods().Returns(pods);
            clock.WindForward(TimeSpan.FromMinutes(checkAfterMinutes));

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            if (shouldDelete)
            {
                await podService.Received().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
                scriptLogProvider.Received().Delete(scriptTicket);
                scriptPodSinceTimeStore.Received().Delete(scriptTicket);
            }
            else
            {
                await podService.DidNotReceiveWithAnyArgs().DeleteIfExists(scriptTicket, Arg.Any<CancellationToken>());
                scriptLogProvider.DidNotReceiveWithAnyArgs().Delete(scriptTicket);
                scriptPodSinceTimeStore.DidNotReceiveWithAnyArgs().Delete(scriptTicket);
            }
        }

        ITrackedScriptPod CreatePod(TrackedScriptPodState state)
        {
            var trackedScriptPod = Substitute.For<ITrackedScriptPod>();
            trackedScriptPod.ScriptTicket.Returns(scriptTicket);
            trackedScriptPod.State.Returns(state);
            
            return trackedScriptPod;
        }
    }
}