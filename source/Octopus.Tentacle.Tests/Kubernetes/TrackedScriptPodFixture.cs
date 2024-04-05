using System;
using System.Collections.Generic;
using FluentAssertions;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class TrackedScriptPodFixture
    {
        [Test]
        public void Foo()
        {
            var scriptTicket = new ScriptTicket("ticketid");
            var trackedPod = new TrackedScriptPod(scriptTicket);
            var finishedAt = new DateTime(2024,1,1, 1,1,1, DateTimeKind.Utc);
            var exitCode = 123;
            
            var podStatus = CreatePodStatus(scriptTicket, finishedAt, exitCode, "Succeeded");
            trackedPod.Update(CreateV1Pod(podStatus));

            trackedPod.ExitCode.Should().Be(exitCode);
            trackedPod.FinishedAt.Should().Be(finishedAt);
            
        }

        static V1PodStatus CreatePodStatus(ScriptTicket scriptTicket, DateTime finishedAt, int exitCode, string phase)
        {
            return new V1PodStatus()
            {
                Phase = phase,
                ContainerStatuses = new List<V1ContainerStatus>()
                {
                    new()
                    {
                        Name = scriptTicket.ToKubernetesScriptPobName(),
                        State = new V1ContainerState()
                        {
                            
                            Terminated = new V1ContainerStateTerminated()
                            {
                                FinishedAt = finishedAt, 
                                ExitCode = exitCode
                            }
                        }
                    }
                }
            };
        }

        static V1Pod CreateV1Pod(V1PodStatus podStatus)
        {
            return new V1Pod(status: podStatus, metadata: new V1ObjectMeta());
        }
    }
}