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
using Octopus.Tentacle.Tests.Support;

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
            var result = await PodLogReader.ReadPodLogs(lastLogSequence, reader,new InMemoryLog());
            result.NextSequenceNumber.Should().Be(lastLogSequence);
            result.Lines.Should().BeEmpty();
        }

        [Test]
        public async Task FirstLine_SequenceNumberIncreasesByOne()
        {
            string[] podLines = {
                "1|2024-04-03T06:03:10.517865655Z|stdout|Kubernetes Script Pod completed",
            };

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(0, reader,new InMemoryLog());
            result.NextSequenceNumber.Should().Be(1);
            result.Lines.Should().BeEquivalentTo(new[]
            {
                new ProcessOutput(ProcessOutputSource.StdOut, "Kubernetes Script Pod completed", DateTimeOffset.Parse("2024-04-03T06:03:10.517865655Z"))
            });
        }

        [Test]
        public async Task ThreeSubsequentLines_SequenceNumberIncreasesByThree()
        {
            string[] podLines = {
                "5|2024-04-03T06:03:10.517857755Z|stdout|##octopus[stdout-verbose]",
                "6|2024-04-03T06:03:10.517865655Z|stderr|Kubernetes Script Pod completed",
                "7|2024-04-03T06:03:10.517867355Z|stdout|##octopus[stdout-default]"
            };

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(4, reader, new InMemoryLog());
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
            string[] podLines = {
                "5|2024-04-03T06:03:10.517857755Z|stdout|##octopus[stdout-verbose]",
                "6|2024-04-03T06:03:10.517865655Z|stderr|Kubernetes Script Pod completed",
                "7|2024-04-03T06:03:10.517867355Z|stdout|##octopus[stdout-default]"
            };

            var allTaskLogs = new List<ProcessOutput>();
            var reader = SetupReader(podLines.Take(1).ToArray());
            var result = await PodLogReader.ReadPodLogs(4, reader,new InMemoryLog());
            result.NextSequenceNumber.Should().Be(5);
            allTaskLogs.AddRange(result.Lines);
            
            reader = SetupReader(podLines.ToArray());
            result = await PodLogReader.ReadPodLogs(5, reader, new InMemoryLog());
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
            string[] podLines =
            {
                "abcdefg",
            };

            var reader = SetupReader(podLines);
            var result = await PodLogReader.ReadPodLogs(0, reader);
            
            result.NextSequenceNumber.Should().Be(0, "The sequence number doesn't move on parse errors");
            var outputLine = result.Lines.Should().ContainSingle().Subject;
            outputLine.Source.Should().Be(ProcessOutputSource.StdErr);
            outputLine.Text.Should().Be("Invalid log line detected. 'abcdefg' is not correctly pipe-delimited.");
            outputLine.Occurred.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        }
        
        [Test]
        public async Task MissingLine_Throws()
        {
            string[] podLines = {
                "100|2024-04-03T06:03:10.517865655Z|stdout|Kubernetes Script Pod completed",
            };
        
            var reader = SetupReader(podLines);
            Func<Task> action = async () => await PodLogReader.ReadPodLogs(50, reader,new InMemoryLog());
            await action.Should().ThrowAsync<MissingPodLogException>();
        }
        
        static StreamReader SetupReader(params string[] lines)
        {
            return new StreamReader(new MemoryStream(Encoding.Default.GetBytes(string.Join("\n", lines))));
        }
    }
}