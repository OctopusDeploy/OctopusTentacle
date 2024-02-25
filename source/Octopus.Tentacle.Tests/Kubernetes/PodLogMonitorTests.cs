using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class PodLogMonitorTests
    {
        [Test]
        public void GetLogsWithZeroSequenceReturnsAllLogLines()
        {
            //arrange
            var podService = Substitute.For<IKubernetesPodService>();
            podService.StreamPodLogs(Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None)
                .Returns(Sequence());

            var monitor = new PodLogMonitor(new V1Pod(), podService);

            //Act
            monitor.StartMonitoring(CancellationToken.None);
            var lines = monitor.GetLogs(0);

//Assert



            IAsyncEnumerable<string> Sequence()
            {
                yield return ""

            }

        }


        static PodLogLine StdOut(string message) =>
    }
}