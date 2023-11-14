using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionScriptServiceV2IsNotRetriedWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task WhenNetworkFailureOccurs_DuringGetCapabilities_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesRecordedUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2>(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        async (_,_) =>
                        {
                            // Due to the GetCapabilities response getting cached, we must
                            // use a different service to ensure Tentacle is connected to Server.
                            // Otherwise, the response to the 'ensure connection' will get cached
                            // and any subsequent calls will succeed w/o using the network.
                            await tcpConnectionUtilities.RestartTcpConnection();

                            if (capabilitiesRecordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException is null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello")).Build();

            var logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("hello");

            var getCapabilitiesUsages = capabilitiesRecordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync));
            getCapabilitiesUsages.LastException.Should().NotBeNull();
            getCapabilitiesUsages.Started.Should().Be(1);
            getCapabilitiesUsages.Completed.Should().Be(1);

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Completed.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Completed.Should().Be(0);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .HookServiceMethod(
                        tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.StartScriptAsync),
                        (_,_) =>
                        {
                            if (scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException is null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }

                            return Task.CompletedTask;
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException.Should().NotBeNull();
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // We must ensure all script are complete, otherwise if we shutdown tentacle while running a script the build can hang.
            // Ensure the script is finished by running the script again
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .HookServiceMethod(
                        tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.GetStatusAsync),
                        (_,_) =>
                        {
                            if (scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException is null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }

                            return Task.CompletedTask;
                        })
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Logger.Information("Starting and waiting for script exec");
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));
            Logger.Information("Exception thrown.");

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException.Should().NotBeNull();
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .HookServiceMethod(
                        tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.GetStatusAsync),
                        async (_,_) =>
                        {
                            await Task.CompletedTask;

                            cts.Cancel();
                        })
                    .HookServiceMethod(
                        tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CancelScriptAsync),
                        async (_,_) =>
                        {
                            if (scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException is null)
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, cts.Token));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException.Should().NotBeNull();
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptRecordedUsages)
                    .HookServiceMethod(
                        tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync),
                        (_,_) =>
                        {
                            if (scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).LastException is null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }

                            return Task.CompletedTask;
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("AllDone");

            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).LastException.Should().NotBeNull();
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            scriptRecordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
        }
    }
}