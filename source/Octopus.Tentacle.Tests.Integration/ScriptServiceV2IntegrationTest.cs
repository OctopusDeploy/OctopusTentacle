using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ScriptServiceV2IntegrationTest
    {
        [Test]
        public async Task CanRunScript()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("Lets do it")
                        .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
                        .Print("All done"))
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));
                var (finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);

                allLogs.Should().Contain("All done");
                allLogs.Should().MatchRegex(".*Lets do it.*another one.*All done.*");

                scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(10);
                scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeLessThan(30);
                logger.Debug("{S}", scriptServiceV2CallCounts.GetStatusCallCountStarted);
                scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
            }
        }

        [Test]
        public async Task DelayInStartScriptSavesNetworkCalls()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("Lets do it")
                        .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
                        .Print("All done"))
                    .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromMinutes(1))
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));
                var (finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);

                allLogs.Should().Contain("All done");
                allLogs.Should().MatchRegex(".*Lets do it.*another one.*All done.*");

                scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().Be(0, "Since start script should wait for the script to finish so we don't need to call get status");
                scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
            }
        }

        [Test]
        public async Task WhenTentacleRestartsWhileRunningAScript_TheExitCodeShouldBe_UnknownResultExitCode()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello")
                        .Sleep(TimeSpan.FromSeconds(1))
                        .Print("waitingtobestopped")
                        .Sleep(TimeSpan.FromSeconds(100)))
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var semaphoreSlim = new SemaphoreSlim(0, 1);

                var executingScript = Task.Run(async () =>
                    await tentacleClient.ExecuteScript(startScriptCommand, token, onScriptStatusResponseReceived =>
                    {
                        if (JoinLogs(onScriptStatusResponseReceived.Logs).Contains("waitingtobestopped"))
                        {
                            semaphoreSlim.Release();
                        }
                    }));

                await semaphoreSlim.WaitAsync(token);

                logger.Information("Stopping and starting tentacle now.");
                await runningTentacle.Restart(token);

                var (finalResponse, logs) = await executingScript;

                finalResponse.Should().NotBeNull();
                JoinLogs(logs).Should().Contain("waitingtobestopped");
                finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running on linux
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.UnknownResultExitCode);

                scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(1);
                scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
            }
        }

        [Test]
        public async Task WhenALongRunningScriptIsCancelled_TheScriptShouldStop()
        {
            var token = TestCancellationToken.Token();
            var logger = new SerilogLoggerBuilder().Build();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello")
                        .Sleep(TimeSpan.FromSeconds(1))
                        .Print("waitingtobestopped")
                        .Sleep(TimeSpan.FromSeconds(100)))
                    .Build();
                
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, octopus, new DefaultScriptObserverBackoffStrategy(), tentacleServicesDecorator, TimeSpan.FromMinutes(4));

                var scriptCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                var stopWatch = Stopwatch.StartNew();
                var (finalResponse, logs) = await tentacleClient.ExecuteScript(startScriptCommand, scriptCancellationTokenSource.Token, onScriptStatusResponseReceived =>
                {
                    if (JoinLogs(onScriptStatusResponseReceived.Logs).Contains("waitingtobestopped"))
                    {
                        scriptCancellationTokenSource.Cancel();
                    }
                });
                stopWatch.Stop();

                finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running.
                finalResponse.ExitCode.Should().NotBe(0);
                stopWatch.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));

                scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
                scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(1);
                scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().BeGreaterThan(0);
                scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
            }
        }

        private static string JoinLogs(List<ProcessOutput> logs)
        {
            return String.Join(" ", logs.Select(l => l.Text).ToArray());
        }
    }
}