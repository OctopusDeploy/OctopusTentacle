using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts.Logging;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptLogFixture
    {
        string logFile;
        ScriptLog sut;
        SensitiveValueMasker sensitiveValueMasker;

        [SetUp]
        public void SetUp()
        {
            logFile = Path.GetTempFileName();
            sensitiveValueMasker = new SensitiveValueMasker();
            sut = new ScriptLog(logFile, new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>()), sensitiveValueMasker);
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
        public void ShouldHandleIncompleteLine()
        {
            using (var appender = sut.CreateWriter())
            {
                appender.WriteOutput(ProcessOutputSource.StdOut, "Hello");
                appender.WriteOutput(ProcessOutputSource.StdOut, "World");
            }

            using (var logFileStream = File.Open(logFile, FileMode.Open))
            {
                var logFileInfo = new FileInfo(logFile);
                logFileStream.SetLength(logFileInfo.Length - 10);
            }
            
            var logs = sut.GetOutput(long.MinValue, out _);
            logs.Count.Should().Be(2);

            logs[1].Text.Should().Be("Corrupt Tentacle log at line 2, no more logs will be read");
        }

        [Test]
        public void ShouldRefuseWritesAfterDisposalRatherThanCorruptTheLog()
        {
            var appender = sut.CreateWriter();
            appender.WriteOutput(ProcessOutputSource.StdOut, "Before disposal");
            appender.Dispose();

            Action writeAfterDisposal = () => appender.WriteOutput(ProcessOutputSource.StdOut, "After disposal");

            writeAfterDisposal.Should().Throw<ObjectDisposedException>();

            var logs = sut.GetOutput(long.MinValue, out _);
            logs.Count.Should().Be(1);
            logs[0].Text.Should().Be("Before disposal");
        }

        [Test]
        public void ShouldReportTheStateOfTheLogWhenItCannotBeParsed()
        {
            using (var appender = sut.CreateWriter())
            {
                appender.WriteOutput(ProcessOutputSource.StdOut, "Hello");
            }

            File.AppendAllText(logFile, "]");

            Action read = () => sut.GetOutput(long.MinValue, out _);

            read.Should().Throw<JsonReaderException>()
                .WithMessage("Could not parse the script log*bytes*")
                .WithInnerException<JsonReaderException>("the parser's own detail is preserved");
        }

        [Test]
        public void ShouldReportThatAWriteWasRefusedAfterDisposal()
        {
            var appender = sut.CreateWriter();
            appender.WriteOutput(ProcessOutputSource.StdOut, "Hello");
            appender.Dispose();

            try
            {
                appender.WriteOutput(ProcessOutputSource.StdOut, "Arrived after the log closed");
            }
            catch (ObjectDisposedException)
            {
                // The refusal is the point; handling it is covered by ShouldRefuseWritesAfterDisposal...
            }

            File.AppendAllText(logFile, "]");

            Action read = () => sut.GetOutput(long.MinValue, out _);

            read.Should().Throw<JsonReaderException>()
                .WithMessage("*a write was refused after it closed*",
                    "this ties an orphaned writer to the corruption without correlating two log sources");
        }

        [Test]
        public void MaskSensitiveValues_SingleMessage_Masked()
        {
            sensitiveValueMasker.WithSensitiveValues(new[] {"abcde"});
            using (var writer = sut.CreateWriter())
            {
                writer.WriteOutput(ProcessOutputSource.Debug, "hello abcde123");

                var logs = sut.GetOutput(0, out long next);

                logs.Should().ContainSingle().Subject.Text.Should().Be("hello ********123");
            }
        }

        //We currently don't mask the first message if a secret spans 2 messages.
        //This shouldn't happen in practice since even when Calamari logs a really long line (10K chars), it won't get split
        //
        //Sample PowerShell script step:
        //        Write-Host "hello start"
        //
        //        for ($i=1; $i -le 10000; $i++) {
        //            $prefix = " " * ($i*2)
        //            Write-Host $prefix $env:HTTP_PROXY
        //        }
        //
        //        Write-Host "hello end"
        [Test]
        public void MaskSensitiveValues_MultiMessage_2ndMessageMasked()
        {
            sensitiveValueMasker.WithSensitiveValues(new[] {"abcde12345"});
            using (var writer = sut.CreateWriter())
            {
                writer.WriteOutput(ProcessOutputSource.Debug, "hello abcde");
                writer.WriteOutput(ProcessOutputSource.Debug, "12345 bye");

                var logs = sut.GetOutput(0, out long next);

                logs.Select(l => l.Text).Should().ContainInOrder("hello abcde", "******** bye");
            }
        }
    }
}