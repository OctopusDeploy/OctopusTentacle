using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Builders;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptServiceFixture
    {
        ScriptService service;

        [SetUp]
        public void SetUp()
        {
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(Environment.CurrentDirectory);

            var octopusPhysicalFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());

            service = new ScriptService(
                PlatformDetection.IsRunningOnWindows ? (IShell) new PowerShell() : new Bash(),
                new ScriptWorkspaceFactory(octopusPhysicalFileSystem, homeConfiguration, new SensitiveValueMasker()),
                new ScriptIsolationMutex(),
                Substitute.For<ISystemLog>());
        }

        [Test]
        public async Task ShouldPingLocalhostSuccessfully()
        {
            var bashPing = "ping localhost -c 1";
            var cmdPing = "& ping.exe localhost -n 1";

            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody(PlatformDetection.IsRunningOnWindows ? cmdPing : bashPing)
                .Build();

            var ticket = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            while ((await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None)).State != ProcessState.Complete)
            {
                Thread.Sleep(100);
            }

            var finalStatus = await service.CompleteScriptAsync(new CompleteScriptCommand(ticket, 0), CancellationToken.None);
            DumpLog(finalStatus);
            Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
            Assert.That(finalStatus.ExitCode, Is.EqualTo(0));
            Assert.That(finalStatus.Logs.Count, Is.GreaterThan(1));
        }

        [Test]
        public async Task ShouldPingRandomUnsuccessfully()
        {
            var guid = Guid.NewGuid();

            var bashPing = $"ping {guid} -c 1";
            var cmdPing = $"& ping.exe {guid} -n 1";

            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody(PlatformDetection.IsRunningOnWindows ? cmdPing : bashPing)
                .Build();

            var ticket = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            while ((await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None)).State != ProcessState.Complete)
            {
                Thread.Sleep(100);
            }

            var finalStatus = await service.CompleteScriptAsync(new CompleteScriptCommand(ticket, 0), CancellationToken.None);
            DumpLog(finalStatus);
            Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
            Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0));
            Assert.That(finalStatus.Logs.Count, Is.GreaterThan(0), "Expected something in the logs");
        }

        [Test]
        public async Task ShouldCancelPing()
        {
            ScriptTicket ticket = null;

            try
            {
                var pollInterval = TimeSpan.FromSeconds(1);
                var safetyLimit = TimeSpan.FromSeconds(120);
                var sw = Stopwatch.StartNew();

                var bashPing = "ping 127.0.0.1 -c 100";
                var cmdPing = "& ping.exe 127.0.0.1 -n 100";

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? cmdPing : bashPing)
                    .Build();

                ticket = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

                ProcessState state;
                Console.WriteLine("Waiting for start");
                while ((state = (await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None)).State) == ProcessState.Pending)
                {
                    Console.WriteLine(state);
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not start in a reasonable time");
                }
                Console.WriteLine("***" + state);

                // Give it a chance to log something
                Console.WriteLine("Waiting for something to get logged");
                while (((await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None)).State) == ProcessState.Running)
                {
                    var status = (await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    if (status.Logs.Any(l => l.Source == ProcessOutputSource.StdOut)) break;
                    Console.WriteLine("...");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not log something in a reasonable time");
                }

                Console.WriteLine("Canceling");
                await service.CancelScriptAsync(new CancelScriptCommand(ticket, 0), CancellationToken.None);

                Console.WriteLine("Waiting for complete");
                while ((state = (await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None)).State) != ProcessState.Complete)
                {
                    var status = (await service.GetStatusAsync(new ScriptStatusRequest(ticket, 0), CancellationToken.None));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not complete in a reasonable time");
                }
                Console.WriteLine("***" + state);

                var finalStatus = await service.CompleteScriptAsync(new CompleteScriptCommand(ticket, 0), CancellationToken.None);
                DumpLog(finalStatus);
                Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
                Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0), "Expected ExitCode to be non-zero");
                Assert.That(finalStatus.Logs.Count, Is.GreaterThan(0), "Expected something in the logs");

                ticket = null;
            }
            finally
            {
                // Try and do our best to clean up the running PowerShell process which can get left open if we fail before attempting to cancel
                if (ticket != null)
                {
                    Console.WriteLine("The test didn't complete successfully. Attempting to cancel the running script which should clean up any dangling processes.");
                    await service.CancelScriptAsync(new CancelScriptCommand(ticket, 0), CancellationToken.None);
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