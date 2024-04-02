using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using k8s;
using k8s.Models;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Tests.Support;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class KubernetesPodMonitorTests
    {
        IKubernetesPodService podService;
        ISystemLog log;
        KubernetesPodMonitor monitor;
        ScriptTicket scriptTicket;
        IClock clock;

        [SetUp]
        public void SetUp()
        {
            podService = Substitute.For<IKubernetesPodService>();
            log = new InMemoryLog();
            clock = new FixedClock(DateTimeOffset.MinValue);
            monitor = new KubernetesPodMonitor(podService, log);

            scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
        }

        [Test]
        public async Task NewlyAddedPodIsAddedToTracking()
        {
            // Arrange
            const WatchEventType type = WatchEventType.Added;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                }
            };

            //Act
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            //Assert
            var status = ((IKubernetesPodStatusProvider)monitor).TryGetPodStatus(scriptTicket);
            status.Should().NotBeNull();

            status.Should().Match<PodStatus>(status =>
                status.ScriptTicket == scriptTicket &&
                status.State == PodState.Running &&
                status.ExitCode == null
            );
        }

        [Test]
        public async Task ExistingPodIsUpdatedWhenCompleted()
        {
            // Arrange
            const WatchEventType type = WatchEventType.Modified;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                }
            };

            podService.ListAllPodsAsync(Arg.Any<CancellationToken>())
                .Returns(new V1PodList
                {
                    Items = new List<V1Pod>
                    {
                        pod
                    }
                });

            //Act

            //preload the pod
            await monitor.InitialLoadAsync(CancellationToken.None);

            //Update the pod
            pod.Status = new V1PodStatus
            {
                Phase = "Succeeded",
                ContainerStatuses = new List<V1ContainerStatus>
                {
                    new()
                    {
                        Name = scriptTicket.ToKubernetesScriptPobName(),
                        State = new V1ContainerState(terminated: new V1ContainerStateTerminated(0))
                    }
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            //Assert
            var status = ((IKubernetesPodStatusProvider)monitor).TryGetPodStatus(scriptTicket);
            status.Should().NotBeNull();

            status.Should().Match<PodStatus>(status =>
                status.ScriptTicket == scriptTicket &&
                status.State == PodState.Succeeded &&
                status.ExitCode == 0
            );
        }

        [Test]
        public async Task ExistingPodIsUpdatedWhenFailed()
        {
            // Arrange
            const WatchEventType type = WatchEventType.Modified;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                }
            };

            podService.ListAllPodsAsync(Arg.Any<CancellationToken>())
                .Returns(new V1PodList
                {
                    Items = new List<V1Pod>
                    {
                        pod
                    }
                });

            //Act

            //preload the pod
            await monitor.InitialLoadAsync(CancellationToken.None);

            //Update the pod
            pod.Status = new V1PodStatus
            {
                Phase = "Failed",
                ContainerStatuses = new List<V1ContainerStatus>
                {
                    new()
                    {
                        Name = scriptTicket.ToKubernetesScriptPobName(),
                        State = new V1ContainerState
                        {
                            Terminated = new V1ContainerStateTerminated
                            {
                                ExitCode = -99
                            }
                        }
                    }
                }
            };
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            //Assert
            var status = ((IKubernetesPodStatusProvider)monitor).TryGetPodStatus(scriptTicket);
            status.Should().NotBeNull();

            status.Should().Match<PodStatus>(status =>
                status.ScriptTicket == scriptTicket &&
                status.State == PodState.Failed &&
                status.ExitCode == -99
            );
        }

        [Test]
        public async Task StopTrackingPodWhenDeleted()
        {
            // Arrange
            const WatchEventType type = WatchEventType.Deleted;
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        [OctopusLabels.ScriptTicketId] = scriptTicket.TaskId
                    }
                }
            };

            podService.ListAllPodsAsync(Arg.Any<CancellationToken>())
                .Returns(new V1PodList
                {
                    Items = new List<V1Pod>
                    {
                        pod
                    }
                });

            //Act

            //preload the pod
            await monitor.InitialLoadAsync(CancellationToken.None);

            //Update the pod
            await monitor.OnNewEvent(type, pod, CancellationToken.None);

            //Assert
            var status = ((IKubernetesPodStatusProvider)monitor).TryGetPodStatus(scriptTicket);
            status.Should().BeNull();
        }
    }
}