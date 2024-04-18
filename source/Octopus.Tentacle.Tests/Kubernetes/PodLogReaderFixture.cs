using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class PodLogReaderFixture
    {
        [TestCase(0, Reason = "Initial position")]
        [TestCase(4, Reason = "Subsequent position")]
        [TestCase(12387126, Reason = "Large position")]
        public async Task NoLines_SameSequenceNumber(long lastLogSequence)
        {
            string[] podLines = Array.Empty<string>();

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(lastLogSequence, reader);
            result.NextSequenceNumber.Should().Be(lastLogSequence);
            result.Lines.Should().BeEmpty();
        }

        [Test]
        public async Task FirstLine_SequenceNumberIncreasesByOne()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517865655Z |1|stdout|Kubernetes Script Pod completed",
            };

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(0, reader);
            result.NextSequenceNumber.Should().Be(1);
            result.Lines.Should().BeEquivalentTo(new[]
            {
                new ProcessOutput(ProcessOutputSource.StdOut, "Kubernetes Script Pod completed", DateTimeOffset.Parse("2024-04-03T06:03:10.517865655Z"))
            });
        }

        [Test]
        public async Task ThreeSubsequentLines_SequenceNumberIncreasesByThree()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517857755Z |5|stdout|##octopus[stdout-verbose]",
                "2024-04-03T06:03:10.517865655Z |6|stderr|Kubernetes Script Pod completed",
                "2024-04-03T06:03:10.517867355Z |7|stdout|##octopus[stdout-default]"
            };

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(4, reader);
            result.NextSequenceNumber.Should().Be(7);
            result.Lines.Should().BeEquivalentTo(new[]
            {
                new ProcessOutput(ProcessOutputSource.StdOut, "##octopus[stdout-verbose]", DateTimeOffset.Parse("2024-04-03T06:03:10.517857755Z")),
                new ProcessOutput(ProcessOutputSource.StdErr, "Kubernetes Script Pod completed", DateTimeOffset.Parse("2024-04-03T06:03:10.517865655Z")),
                new ProcessOutput(ProcessOutputSource.StdOut, "##octopus[stdout-default]", DateTimeOffset.Parse("2024-04-03T06:03:10.517867355Z")),
            });
        }

        [Test]
        public async Task StreamContainsPreviousLines_Deduplicates()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517857755Z |5|stdout|##octopus[stdout-verbose]",
                "2024-04-03T06:03:10.517865655Z |6|stderr|Kubernetes Script Pod completed",
                "2024-04-03T06:03:10.517867355Z |7|stdout|##octopus[stdout-default]"
            };

            var allTaskLogs = new List<ProcessOutput>();
            var reader = SetupReader(podLines.Take(1).ToArray());
            var result = await PodLogReader.ReadPodLogs(4, reader);
            result.NextSequenceNumber.Should().Be(5);
            allTaskLogs.AddRange(result.Lines);

            reader = SetupReader(podLines.ToArray());
            result = await PodLogReader.ReadPodLogs(5, reader);
            result.NextSequenceNumber.Should().Be(7);
            allTaskLogs.AddRange(result.Lines);

            allTaskLogs.Should().BeEquivalentTo(new[]
            {
                new ProcessOutput(ProcessOutputSource.StdOut, "##octopus[stdout-verbose]", DateTimeOffset.Parse("2024-04-03T06:03:10.517857755Z")),
                new ProcessOutput(ProcessOutputSource.StdErr, "Kubernetes Script Pod completed", DateTimeOffset.Parse("2024-04-03T06:03:10.517865655Z")),
                new ProcessOutput(ProcessOutputSource.StdOut, "##octopus[stdout-default]", DateTimeOffset.Parse("2024-04-03T06:03:10.517867355Z")),
            });
        }

        [Test]
        public async Task ParseError_AppearsAsError()
        {
            var line = "abcdefg";

            var reader = SetupReader(new[] { line });
            var result = await PodLogReader.ReadPodLogs(0, reader);

            result.NextSequenceNumber.Should().Be(0, "The sequence number doesn't move on parse errors");
            var outputLine = result.Lines.Should().ContainSingle().Subject;
            outputLine.Source.Should().Be(ProcessOutputSource.StdErr);
            outputLine.Text.Should().Contain("not correctly pipe-delimited").And.Contain(line);
            outputLine.Occurred.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Test]
        public async Task MissingLine_Throws()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517865655Z |100|stdout|Kubernetes Script Pod completed",
            };

            var reader = SetupReader(podLines);
            Func<Task> action = async () => await PodLogReader.ReadPodLogs(50, reader);
            await action.Should().ThrowAsync<UnexpectedPodLogLineNumberException>();
        }

        [Test]
        public async Task LineOutOfOrderAtStart_Throws()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517865655Z |5|stdout|Kubernetes Script Pod completed",
                "2024-04-03T06:03:10.517865655Z |4|stderr|Kubernetes Script Pod completed",
            };

            var reader = SetupReader(podLines);
            Func<Task> action = async () => await PodLogReader.ReadPodLogs(4, reader);
            await action.Should().ThrowAsync<UnexpectedPodLogLineNumberException>();
        }

        [Test]
        public async Task LineOutOfOrderMidway_Throws()
        {
            string[] podLines =
            {
                "2024-04-03T06:03:10.517865655Z |3|stdout|Kubernetes Script Pod completed",
                "2024-04-03T06:03:10.517865655Z |5|stdout|Kubernetes Script Pod completed",
                "2024-04-03T06:03:10.517865655Z |4|stderr|Kubernetes Script Pod completed",
            };

            var reader = SetupReader(podLines);
            Func<Task> action = async () => await PodLogReader.ReadPodLogs(2, reader);
            await action.Should().ThrowAsync<UnexpectedPodLogLineNumberException>();
        }

        static StreamReader SetupReader(params string[] lines)
        {
            return new StreamReader(new MemoryStream(Encoding.Default.GetBytes(string.Join("\n", lines))));
        }
    }
}