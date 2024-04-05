using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class PodLogLineParserFixture
    {
        [TestCase("a|b|c", Reason = "Doesn't have 4 parts")]
        public void NotCorrectlyPipeDelimited(string line)
        {
            var result = PodLogLineParser.ParseLine(line).Should().BeOfType<InvalidPodLogLineParseResult>().Subject;
            result.Error.Should().Contain("delimited");
        }
        
        [TestCase("a|b|c|d", Reason = "Not a line number")]
        public void FirstPartIsNotALineNumber(string line)
        {
            var result = PodLogLineParser.ParseLine(line).Should().BeOfType<InvalidPodLogLineParseResult>().Subject;
            result.Error.Should().Contain("line number");
        }
        
        [TestCase("1|b|c|d", Reason = "Not a date")]
        public void SecondPartIsNotALineDate(string line)
        {
            var result = PodLogLineParser.ParseLine(line).Should().BeOfType<InvalidPodLogLineParseResult>().Subject;
            result.Error.Should().Contain("DateTimeOffset");
        }
        
        [TestCase("1|2024-04-03T06:03:10.501025551Z|c|d", Reason = "Not a valid source")]
        public void ThirdPartIsNotAValidSource(string line)
        {
            var result = PodLogLineParser.ParseLine(line).Should().BeOfType<InvalidPodLogLineParseResult>().Subject;
            result.Error.Should().Contain("source");
        }
        
        [Test]
        public void SimpleMessage()
        {
            var logLine = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|This is the message")
                .Should().BeOfType<ValidPodLogLineParseResult>().Subject.LogLine;

            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.StdOut);
            logLine.Message.Should().Be("This is the message");
            logLine.Occurred.Should().BeCloseTo(new DateTimeOffset(2024, 4, 3, 6, 3, 10, 501, TimeSpan.Zero), TimeSpan.FromMilliseconds(1));
        }
        
        [Test]
        public void ServiceMessage()
        {
            var logLine = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|##octopus[stdout-verbose]")
                .Should().BeOfType<ValidPodLogLineParseResult>().Subject.LogLine;

            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.StdOut);
            logLine.Message.Should().Be("##octopus[stdout-verbose]");
            logLine.Occurred.Should().BeCloseTo(new DateTimeOffset(2024, 4, 3, 6, 3, 10, 501, TimeSpan.Zero), TimeSpan.FromMilliseconds(1));
        }
        
        [Test]
        public void ErrorMessage()
        {
            var logLine = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stderr|Error!")
                .Should().BeOfType<ValidPodLogLineParseResult>().Subject.LogLine;

            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.StdErr);
            logLine.Message.Should().Be("Error!");
            logLine.Occurred.Should().BeCloseTo(new DateTimeOffset(2024, 4, 3, 6, 3, 10, 501, TimeSpan.Zero), TimeSpan.FromMilliseconds(1));
        }
        
        [Test]
        public void MessageHasPipeInIt()
        {
            var logLine = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|This is the me|ss|age")
                .Should().BeOfType<ValidPodLogLineParseResult>().Subject.LogLine;

            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.StdOut);
            logLine.Message.Should().Be("This is the me|ss|age");
            logLine.Occurred.Should().BeCloseTo(new DateTimeOffset(2024, 4, 3, 6, 3, 10, 501, TimeSpan.Zero), TimeSpan.FromMilliseconds(1));
        }
        
        [Test]
        public void ValidEndOfStreamWithPositiveExitCode()
        {
            var result = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>4")
                .Should().BeOfType<EndOfStreamPodLogLineParseResult>().Subject;

            result.ExitCode.Should().Be(4);
            
            var logLine = result.LogLine;
            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.Debug);
            logLine.Message.Should().Be("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>4");
        }
        
        [Test]
        public void ValidEndOfStreamWithNegativeExitCode()
        {
            var result = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>-64")
                .Should().BeOfType<EndOfStreamPodLogLineParseResult>().Subject;

            result.ExitCode.Should().Be(-64);
            
            var logLine = result.LogLine;
            logLine.LineNumber.Should().Be(123);
            logLine.Source.Should().Be(ProcessOutputSource.Debug);
            logLine.Message.Should().Be("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>-64");
        }
        
        [Test]
        public void InvalidEndOfStream()
        {
            var result = PodLogLineParser.ParseLine("123|2024-04-03T06:03:10.501025551Z|stdout|EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>")
                .Should().BeOfType<InvalidPodLogLineParseResult>().Subject;

            result.Error.Should().Contain("end of stream");
        }
    }
}