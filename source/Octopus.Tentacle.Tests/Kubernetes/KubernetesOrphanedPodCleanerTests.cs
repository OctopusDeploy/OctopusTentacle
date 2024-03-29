using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using k8s;
using k8s.Models;
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
        KubernetesPodMonitor monitor;
        ScriptTicket scriptTicket;
        KubernetesOrphanedPodCleaner cleaner;
        TimeSpan overCutoff;
        TimeSpan underCutoff;

        [SetUp]
        public void Setup()
        {
            podService = Substitute.For<IKubernetesPodService>();
            log = new InMemoryLog();
            clock = new FixedClock(DateTimeOffset.MinValue + 1.Days());
            monitor = new KubernetesPodMonitor(podService, log, clock);

            scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());

            cleaner = new KubernetesOrphanedPodCleaner(monitor, podService, log, clock);

            overCutoff = cleaner.CompletedPodConsideredOrphanedAfterTimeSpan + 1.Minutes();
            underCutoff = cleaner.CompletedPodConsideredOrphanedAfterTimeSpan - 1.Minutes();
        }

        [Test]
        public async Task OrphanedPodCleanedUpIfOver10MinutesHavePassed()
        {
            //Arrange
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = "Succeeded"
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.Received().Delete(scriptTicket, Arg.Any<CancellationToken>());
        }

        [TestCase("Succeeded", true)]
        [TestCase("Failed", true)]
        [TestCase("Running", false)]
        [TestCase(null, false)]
        public async Task OrphanedPodOnlyCleanedUpWhenNotRunning(string? phase, bool shouldBeDeleted)
        {
            //Arrange
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = phase
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            if (shouldBeDeleted)
            {
                await podService.Received().Delete(scriptTicket, Arg.Any<CancellationToken>());
            }
            else
            {
                await podService.DidNotReceive().Delete(scriptTicket, Arg.Any<CancellationToken>());
            }
        }

        [Test]
        public async Task OrphanedPodNotCleanedUpIfOnly9MinutesHavePassed()
        {
            //Arrange
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = "Succeeded"
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            clock.WindForward(underCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.DidNotReceive().Delete(scriptTicket, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OrphanedPodNotCleanedUpIfPodCleanupIsDisabled()
        {
            //Arrange
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__DISABLEAUTOPODCLEANUP", "true");
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = "Succeeded"
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            clock.WindForward(overCutoff);

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            await podService.DidNotReceive().Delete(scriptTicket, Arg.Any<CancellationToken>());

            //Cleanup
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__DISABLEAUTOPODCLEANUP", null);
        }

        [TestCase(1, false)]
        [TestCase(3, true)]
        public async Task EnvironmentVariableDictatesWhenPodsAreConsideredOrphaned(int checkAfterMinutes, bool shouldDelete)
        {
            //Arrange
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__PODSCONSIDEREDORPHANEDAFTERMINUTES", "2");

            // We need to reinitialise the sut after changing the env var value
            cleaner = new KubernetesOrphanedPodCleaner(monitor, podService, log, clock);
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                },
                Status = new V1PodStatus
                {
                    Phase = "Succeeded"
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            clock.WindForward(TimeSpan.FromMinutes(checkAfterMinutes));

            //Act
            await cleaner.CheckForOrphanedPods(CancellationToken.None);

            //Assert
            if (shouldDelete)
            {
                await podService.Received().Delete(scriptTicket, Arg.Any<CancellationToken>());
            }
            else
            {
                await podService.DidNotReceive().Delete(scriptTicket, Arg.Any<CancellationToken>());
            }

            //Cleanup
            Environment.SetEnvironmentVariable("OCTOPUS__K8STENTACLE__PODSCONSIDEREDORPHANEDAFTERMINUTES", null);
        }
    }
}