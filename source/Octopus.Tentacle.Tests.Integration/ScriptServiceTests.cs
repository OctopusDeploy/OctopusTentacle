using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Services.Scripts
{
    public class ScriptServiceTests
    {
        [Test]
        public async Task RunScriptWithSuccessOnPollingTentacle()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Write-Host ""The answer is"" (6 * 7)
                Start-Sleep -Seconds 3
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                val=6
                ((theAnswer=$val*7))
                echo The answer is $theAnswer
                sleep 3
                echo This is the end of the script";

            var finalStatus = await RunScriptOnLocalPollingTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().Be(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("The answer is 42");
        }

        [Test]
        public async Task RunScriptWithErrorsOnPollingTentacle()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Start-Sleep -Seconds 3
                throw ""Whoopsy Daisy!""
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                sleep 3
                echo ""Whoopsy Daisy!""
                exit 1
                echo This is the end of the script""";

            var finalStatus = await RunScriptOnLocalPollingTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().NotBe(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("Whoopsy Daisy!");
            finalStatus.Logs.Select(x => x.Text).Should().NotContain("This is the end of the script");
        }

        [Test]
        public async Task RunScriptWithSuccessOnListeningTentacle()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Write-Host ""The answer is"" (6 * 7)
                Start-Sleep -Seconds 3
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                val=6
                ((theAnswer=$val*7))
                echo The answer is $theAnswer
                sleep 3
                echo This is the end of the script";

            var finalStatus = await RunScriptOnLocalListeningTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().Be(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("The answer is 42");
        }

        [Test]
        public async Task RunScriptWithErrorsOnListeningTentacle()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Start-Sleep -Seconds 3
                throw ""Whoopsy Daisy!""
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                sleep 3
                echo ""Whoopsy Daisy!""
                exit 1
                echo This is the end of the script""";

            var finalStatus = await RunScriptOnLocalListeningTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().NotBe(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("Whoopsy Daisy!");
            finalStatus.Logs.Select(x => x.Text).Should().NotContain("This is the end of the script");
        }

        [Test]
        public async Task CancelScriptOnPollingTentacle()
        {
            var pollInterval = TimeSpan.FromSeconds(1);
            var safetyLimit = TimeSpan.FromSeconds(120);
            var sw = Stopwatch.StartNew();

            var bashScript = "ping 127.0.0.1 -c 100";
            var cmdScript = "& ping.exe 127.0.0.1 -n 100";

            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? cmdScript : bashScript)
                    .Build();

                // Start script
                var ticket = tentacleClient.ScriptService.StartScript(startScriptCommand);
                ProcessState state;
                Console.WriteLine("Waiting for start");
                while ((state = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Pending)
                {
                    Console.WriteLine(state);
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not start in a reasonable time");
                }

                Console.WriteLine("***" + state);

                // Give it a chance to log something
                Console.WriteLine("Waiting for something to get logged");
                while ((tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Running)
                {
                    var status = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    if (status.Logs.Any(l => l.Source == ProcessOutputSource.StdOut)) break;
                    Console.WriteLine("...");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not log something in a reasonable time");
                }

                Console.WriteLine("Canceling");
                tentacleClient.ScriptService.CancelScript(new CancelScriptCommand(ticket, 0));

                Console.WriteLine("Waiting for complete");
                while ((state = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) != ProcessState.Complete)
                {
                    var status = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not complete in a reasonable time");
                }

                Console.WriteLine("***" + state);

                var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(ticket, 0));
                DumpLog(finalStatus);
                Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
                Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0), "Expected ExitCode to be non-zero");
                Assert.That(finalStatus.Logs.Count, Is.GreaterThan(0), "Expected something in the logs");
            }
        }

        [Test]
        public async Task CancelScriptOnListeningTentacle()
        {
            var pollInterval = TimeSpan.FromSeconds(1);
            var safetyLimit = TimeSpan.FromSeconds(120);
            var sw = Stopwatch.StartNew();

            var bashScript = "ping 127.0.0.1 -c 100";
            var cmdScript = "& ping.exe 127.0.0.1 -n 100";

            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new ListeningTentacleBuilder(Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? cmdScript : bashScript)
                    .Build();

                // Start script
                var ticket = tentacleClient.ScriptService.StartScript(startScriptCommand);
                ProcessState state;
                Console.WriteLine("Waiting for start");
                while ((state = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Pending)
                {
                    Console.WriteLine(state);
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not start in a reasonable time");
                }

                Console.WriteLine("***" + state);

                // Give it a chance to log something
                Console.WriteLine("Waiting for something to get logged");
                while ((tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) == ProcessState.Running)
                {
                    var status = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    if (status.Logs.Any(l => l.Source == ProcessOutputSource.StdOut)) break;
                    Console.WriteLine("...");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not log something in a reasonable time");
                }

                Console.WriteLine("Canceling");
                tentacleClient.ScriptService.CancelScript(new CancelScriptCommand(ticket, 0));

                Console.WriteLine("Waiting for complete");
                while ((state = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0)).State) != ProcessState.Complete)
                {
                    var status = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(ticket, 0));
                    Console.WriteLine($"{status.State} ({sw.Elapsed} elapsed)");
                    Thread.Sleep(pollInterval);
                    if (sw.Elapsed > safetyLimit) Assert.Fail("Did not complete in a reasonable time");
                }

                Console.WriteLine("***" + state);

                var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(ticket, 0));
                DumpLog(finalStatus);
                Assert.That(finalStatus.State, Is.EqualTo(ProcessState.Complete));
                Assert.That(finalStatus.ExitCode, Is.Not.EqualTo(0), "Expected ExitCode to be non-zero");
                Assert.That(finalStatus.Logs.Count, Is.GreaterThan(0), "Expected something in the logs");
            }
        }

        async Task<ScriptStatusResponse> RunScriptOnLocalPollingTentacle(string psScript, string bashScript)
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? psScript : bashScript)
                    .Build();

                var scriptTicket = tentacleClient.ScriptService.StartScript(startScriptCommand);
                while (tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptTicket, 0)).State != ProcessState.Complete)
                {
                    Thread.Sleep(100);
                }

                var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(scriptTicket, 0));

                DumpLog(finalStatus);

                return finalStatus;
            }
        }

        async Task<ScriptStatusResponse> RunScriptOnLocalListeningTentacle(string psScript, string bashScript)
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new ListeningTentacleBuilder(Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? psScript : bashScript)
                    .Build();

                var scriptTicket = tentacleClient.ScriptService.StartScript(startScriptCommand);
                while (tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptTicket, 0)).State != ProcessState.Complete)
                {
                    Thread.Sleep(100);
                }

                var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(scriptTicket, 0));

                DumpLog(finalStatus);

                return finalStatus;
            }
        }

        private static void DumpLog(ScriptStatusResponse finalStatus)
        {
            Console.WriteLine("### Start of script result logs ###");
            foreach (var log in finalStatus.Logs)
            {
                Console.WriteLine(log.Text);
            }

            Console.WriteLine("### End of script result logs ###");
        }
    }
}
