using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Scripts;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration.Proxy;
using Octopus.Tentacle.Services.Scripts;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptServiceFixture
    {
        IScriptService service;

        [SetUp]
        public void SetUp()
        {
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(Environment.CurrentDirectory);

            service = new ScriptService(PlatformDetection.IsRunningOnWindows 
                ? (IShell) new PowerShell() 
                : new Bash()
                , new ScriptWorkspaceFactory(new OctopusPhysicalFileSystem(), homeConfiguration), new OctopusPhysicalFileSystem(), new SensitiveValueMasker(new LogContext()));
        }

        [Test]
        public void ShouldPingLocalhostSuccessfully()
        {
            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody("& ping.exe localhost -n 1")
                .Build();

            var ticket = service.StartScript(startScriptCommand);

            while (service.GetStatus(new ScriptStatusRequest(ticket, 0)).State != ProcessState.Complete)
            {
                Thread.Sleep(100);
            }

            var finalStatus = service.CompleteScript(new CompleteScriptCommand(ticket, 0));
            DumpLog(finalStatus);
            Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
            Assert.That(finalStatus.ExitCode, Is.EqualTo(0));
            Assert.That(finalStatus.Logs.Count, Is.GreaterThan(1));
        }

        [Test]
        public void ShouldPingRandomUnsuccessfully()
        {
            var guid = Guid.NewGuid();
            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody($"& ping.exe {guid} -n 1")
                .Build();

            var ticket = service.StartScript(startScriptCommand);

            while (service.GetStatus(new ScriptStatusRequest(ticket, 0)).State != ProcessState.Complete)
            {
                Thread.Sleep(100);
            }

            var finalStatus = service.CompleteScript(new CompleteScriptCommand(ticket, 0));
            DumpLog(finalStatus);
            Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
            Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0));
            finalStatus.Logs.Select(l => l.Text).Should().Contain($"Ping request could not find host {guid}. Please check the name and try again.");
        }

        [Test]
        public void ShouldCancelPing()
        {
            ScriptTicket ticket = null;

            try
            {
                var pollInterval = 100;
                var safetyLimit = (20 * 1000) / pollInterval;

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody("& ping.exe 127.0.0.1 -n 100")
                    .Build();

                ticket = service.StartScript(startScriptCommand);

                ProcessState state;
                Console.WriteLine("Waiting for start");
                while ((state = service.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Pending)
                {
                    Console.WriteLine(state);
                    Thread.Sleep(pollInterval);
                    if (safetyLimit-- == 0) Assert.Fail("Did not start in a reasonable time");
                }
                Console.WriteLine("***" + state);

                // Give it a chance to log something
                Console.WriteLine("Waiting for something to get logged");
                while ((service.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Running)
                {
                    var status = service.GetStatus(new ScriptStatusRequest(ticket, 0));
                    if (status.Logs.Any(l => l.Source == ProcessOutputSource.StdOut)) break;
                    Console.WriteLine("...");
                    Thread.Sleep(pollInterval);
                    if (safetyLimit-- == 0) Assert.Fail("Did not log something in a reasonable time");
                }

                Console.WriteLine("Canceling");
                service.CancelScript(new CancelScriptCommand(ticket, 0));

                Console.WriteLine("Waiting for complete");
                while ((state = service.GetStatus(new ScriptStatusRequest(ticket, 0)).State) != ProcessState.Complete)
                {
                    Console.WriteLine(state);
                    Thread.Sleep(pollInterval);
                    if (safetyLimit-- == 0) Assert.Fail("Did not complete in a reasonable time");
                }
                Console.WriteLine("***" + state);

                var finalStatus = service.CompleteScript(new CompleteScriptCommand(ticket, 0));
                DumpLog(finalStatus);
                Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
                Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0), "Expected ExitCode to be non-zero");
                Assert.That(finalStatus.Logs.Count, Is.GreaterThan(0), "Expected something in the logs");

                ticket = null;
            }
            finally
            {
                // Try and do our best to clean up the running powershell process which can get left open if we fail before attempting to cancel
                if (ticket != null)
                {
                    Console.WriteLine("The test didn't complete successfully. Attempting to cancel the running script which should clean up any dangling processes.");
                    service.CancelScript(new CancelScriptCommand(ticket, 0));
                    ticket = null;
                }
            }
        }

        void DumpLog(ScriptStatusResponse finalStatus)
        {
            foreach (var log in finalStatus.Logs)
            {
                Console.WriteLine(log.Text);
            }
        }
    }
}