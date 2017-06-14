using System;
using System.IO;
using NUnit.Framework;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;
using Octopus.Tentacle.Services.Scripts;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptLogFixture
    {
        [SetUp]
        public void SetUp()
        {
        }

        [Test]
        public void ShouldAppend()
        {
            var logFile = Path.GetTempFileName();
            try
            {
                var log = new ScriptLog(logFile, new OctopusPhysicalFileSystem());

                var appender = log.CreateWriter();
                appender.WriteOutput(ProcessOutputSource.StdOut, "Hello");
                appender.WriteOutput(ProcessOutputSource.StdOut, "World");

                long next;
                var logs = log.GetOutput(long.MinValue, out next);
                Assert.That(logs.Count, Is.EqualTo(2));
                Assert.That(logs[0].Text, Is.EqualTo("Hello"));
                Assert.That(logs[0].Source, Is.EqualTo(ProcessOutputSource.StdOut));
                Assert.That(logs[1].Text, Is.EqualTo("World"));

                appender.WriteOutput(ProcessOutputSource.StdOut, "More");
                appender.WriteOutput(ProcessOutputSource.StdOut, "Output");

                logs = log.GetOutput(next, out next);
                Assert.That(logs.Count, Is.EqualTo(2));
                Assert.That(logs[0].Text, Is.EqualTo("More"));
                Assert.That(logs[1].Text, Is.EqualTo("Output"));

                appender.WriteOutput(ProcessOutputSource.StdErr, "ErrorHappened");

                logs = log.GetOutput(next, out next);
                Assert.That(logs.Count, Is.EqualTo(1));
                Assert.That(logs[0].Text, Is.EqualTo("ErrorHappened"));
                Assert.That(logs[0].Source, Is.EqualTo(ProcessOutputSource.StdErr));

                appender.Dispose();
            }
            finally
            {
                File.Delete(logFile);
            }
        }
    }
}