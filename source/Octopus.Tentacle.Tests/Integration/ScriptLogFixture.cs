using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Octopus.Tentacle.Services.Scripts;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptLogFixture
    {
        string logFile;
        ScriptLog sut;
        LogContext logContext;

        [SetUp]
        public void SetUp()
        {
            logFile = Path.GetTempFileName();
            logContext = new LogContext();
            sut = new ScriptLog(logFile, new OctopusPhysicalFileSystem(), logContext);
        }

        [TearDown]
        public void TearDown()
        {
            if (logFile != null)
                File.Delete(logFile);
        }

        [Test]
        public void ShouldAppend()
        {

            using (var appender = sut.CreateWriter())
            {
                appender.WriteOutput(ProcessOutputSource.StdOut, "Hello");
                appender.WriteOutput(ProcessOutputSource.StdOut, "World");

                long next;
                var logs = sut.GetOutput(long.MinValue, out next);
                Assert.That(logs.Count, Is.EqualTo(2));
                Assert.That(logs[0].Text, Is.EqualTo("Hello"));
                Assert.That(logs[0].Source, Is.EqualTo(ProcessOutputSource.StdOut));
                Assert.That(logs[1].Text, Is.EqualTo("World"));

                appender.WriteOutput(ProcessOutputSource.StdOut, "More");
                appender.WriteOutput(ProcessOutputSource.StdOut, "Output");

                logs = sut.GetOutput(next, out next);
                Assert.That(logs.Count, Is.EqualTo(2));
                Assert.That(logs[0].Text, Is.EqualTo("More"));
                Assert.That(logs[1].Text, Is.EqualTo("Output"));

                appender.WriteOutput(ProcessOutputSource.StdErr, "ErrorHappened");

                logs = sut.GetOutput(next, out next);
                Assert.That(logs.Count, Is.EqualTo(1));
                Assert.That(logs[0].Text, Is.EqualTo("ErrorHappened"));
                Assert.That(logs[0].Source, Is.EqualTo(ProcessOutputSource.StdErr));
            }
        }

        [Test]
        public void MaskSensitiveValues()
        {
            logContext.WithSensitiveValues(new[] {"abcde"});
            using (var writer = sut.CreateWriter())
            {
                writer.WriteOutput(ProcessOutputSource.Debug, "hello abcde123");

                var logs = sut.GetOutput(0, out long next);

                logs.Should().ContainSingle().Subject.Text.Should().Be("hello ********123");
            }
        }
    }
}