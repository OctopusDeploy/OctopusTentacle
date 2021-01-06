﻿using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration.Proxy;
using Octopus.Tentacle.Services.Scripts;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptLogFixture
    {
        string logFile;
        ScriptLog sut;
        ISensitiveValueMasker sensitiveValueMasker;
        LogContext logContext;

        [SetUp]
        public void SetUp()
        {
            logFile = Path.GetTempFileName();
            logContext = new LogContext();
            sensitiveValueMasker = new SensitiveValueMasker(logContext);
            sut = new ScriptLog(logFile, new OctopusPhysicalFileSystem(), sensitiveValueMasker);
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
        public void MaskSensitiveValues_SingleMessage_Masked()
        {
            logContext.WithSensitiveValues(new[] {"abcde"});
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
            logContext.WithSensitiveValues(new[] {"abcde12345"});
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