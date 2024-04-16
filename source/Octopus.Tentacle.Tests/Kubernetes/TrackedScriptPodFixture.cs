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
        TrackedScriptPod trackedPod;
        ScriptTicket scriptTicket;

        [SetUp]
        public void SetUp()
        {
            scriptTicket = new ScriptTicket("ScriptTicketId");
            trackedPod = new TrackedScriptPod(scriptTicket);
        }

        [TestCase(PodPhases.Succeeded, 0, TrackedScriptPodState.Succeeded)]
        [TestCase(PodPhases.Failed, 123, TrackedScriptPodState.Failed)]
        public void UpdateWithCompletedPod(string podPhase, int exitCode, TrackedScriptPodState expectedState)
        {
            var finishedAt = new DateTime(2024, 1, 1, 1, 1, 1, DateTimeKind.Utc);

            GetPodInRunningState();

            trackedPod.Update(CreateV1Pod(podPhase, TerminatedContainerState(finishedAt, exitCode)));

            trackedPod.State.Should().Be(expectedState);
            trackedPod.ExitCode.Should().Be(exitCode);
            trackedPod.FinishedAt.Should().Be(finishedAt);
        }

        [TestCase(0, TrackedScriptPodState.Succeeded)]
        [TestCase(123, TrackedScriptPodState.Failed)]
        public void MarkAsCompleted(int exitCode, TrackedScriptPodState expectedState)
        {
            var finishedAt = new DateTime(2024, 1, 1, 1, 1, 1, DateTimeKind.Utc);

            GetPodInRunningState();

            trackedPod.MarkAsCompleted(exitCode, finishedAt);

            trackedPod.State.Should().Be(expectedState);
            trackedPod.ExitCode.Should().Be(exitCode);
            trackedPod.FinishedAt.Should().Be(finishedAt);
        }

        //When Tentacle restarts we will re-read all Pod statuses from the K8s API, so there's no point trying to prevent the tracked Pod
        //from changing state once it has completed
        [Test]
        public void PodUpdateAfterMarkAsCompleted_UpdatesToNewValue()
        {
            var markAsCompletedExitCode = 0;
            var markAsCompletedFinishedAt = new DateTime(2024, 1, 1, 1, 1, 1, DateTimeKind.Utc);

            GetPodInRunningState();

            trackedPod.MarkAsCompleted(markAsCompletedExitCode, markAsCompletedFinishedAt);

            trackedPod.State.Should().Be(TrackedScriptPodState.Succeeded);
            trackedPod.ExitCode.Should().Be(markAsCompletedExitCode);
            trackedPod.FinishedAt.Should().Be(markAsCompletedFinishedAt);

            var podStatusExitCode = 1;
            var podStatusFinishedAt = new DateTime(2024, 1, 1, 1, 1, 5, DateTimeKind.Utc);

            trackedPod.Update(CreateV1Pod(PodPhases.Failed, TerminatedContainerState(podStatusFinishedAt, podStatusExitCode)));

            trackedPod.State.Should().Be(TrackedScriptPodState.Failed);
            trackedPod.ExitCode.Should().Be(podStatusExitCode);
            trackedPod.FinishedAt.Should().Be(podStatusFinishedAt);
        }
        
        void GetPodInRunningState()
        {
            trackedPod.Update(CreateV1Pod(PodPhases.Running, RunningContainerState()));

            trackedPod.State.Should().Be(TrackedScriptPodState.Running);
            trackedPod.ExitCode.Should().BeNull();
            trackedPod.FinishedAt.Should().BeNull();
        }

        static V1ContainerState RunningContainerState()
        {
            return new V1ContainerState()
            {

                Running = new V1ContainerStateRunning()
            };
        }

        static V1ContainerState TerminatedContainerState(DateTime finishedAt, int exitCode)
        {
            return new V1ContainerState()
            {

                Terminated = new V1ContainerStateTerminated()
                {
                    FinishedAt = finishedAt,
                    ExitCode = exitCode
                }
            };
        }

        V1Pod CreateV1Pod(string phase, V1ContainerState containerState)
        {
            var podStatus = new V1PodStatus()
            {
                Phase = phase,
                ContainerStatuses = new List<V1ContainerStatus>()
                {
                    new()
                    {
                        Name = scriptTicket.ToKubernetesScriptPodName(),
                        State = containerState
                    }
                }
            };

            return new V1Pod(status: podStatus, metadata: new V1ObjectMeta());
        }
    }
}