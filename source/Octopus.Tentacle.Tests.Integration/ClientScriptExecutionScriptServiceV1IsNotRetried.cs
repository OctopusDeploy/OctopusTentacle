using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using Halibut.Util;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionScriptServiceV1IsNotRetried : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testNoCapabilitiesServiceVersions: true)]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
                        .BeforeStartScript(async () =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceExceptions.StartScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithIsolation(ScriptIsolationLevel.FullIsolation)
                .WithMutexName("bob")
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.StartScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // We must ensure all script are complete, otherwise if we shutdown tentacle while running a script the build can hang.
            // Ensure the script is finished by running the script again, the isolation mutex will ensure this second script runs after the first is complete.
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, new List<ProcessOutput>(), CancellationToken);
        }

        [Test]
        [TentacleConfigurations(testNoCapabilitiesServiceVersions: true)]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            ScriptStatusRequest? scriptStatusRequest = null;
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
                        .BeforeGetStatus(async (inner, request) =>
                        {
                            await Task.CompletedTask;

                            scriptStatusRequest = request;
                            if (scriptServiceExceptions.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Logger.Information("Starting and waiting for script exec");
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));
            Logger.Information("Exception thrown.");

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            var legacyTentacleClient = clientTentacle.LegacyTentacleClientBuilder(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature()).Build(CancellationToken);

            if (tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature().IsDisabled())
            {
                await Wait.For(() => legacyTentacleClient.ScriptService.SyncService.GetStatus(scriptStatusRequest).State == ProcessState.Complete, CancellationToken);
            }
            else
            {
                await Wait.For(async () => (await legacyTentacleClient.ScriptService.AsyncService.GetStatusAsync(scriptStatusRequest, new(CancellationToken, null)))
                    .State == ProcessState.Complete, CancellationToken);
            }

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.GetStatusLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);
        }

        [Test]
        [TentacleConfigurations(testNoCapabilitiesServiceVersions: true)]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            ScriptStatusRequest? scriptStatusRequest = null;
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithPortForwarderDataLogging()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
                        .BeforeGetStatus(async (_, request) =>
                        {
                            await Task.CompletedTask;

                            cts.Cancel();
                            scriptStatusRequest = request;
                        })
                        .BeforeCancelScript(async (service, _) =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceExceptions.CancelScriptLatestException == null)
                            {
                                await service.EnsureTentacleIsConnectedToServer(Logger);
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            var logs = new List<ProcessOutput>();
            
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, cts.Token));
            
            var allLogs = logs.JoinLogs();
            allLogs.Should().NotContain("AllDone");
            scriptServiceExceptions.CancelScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");
            var legacyTentacleClient = clientTentacle.LegacyTentacleClientBuilder(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature()).Build(CancellationToken);

            if (tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature().IsDisabled())
            {
                await Wait.For(() => legacyTentacleClient.ScriptService.SyncService.GetStatus(scriptStatusRequest).State == ProcessState.Complete, CancellationToken);
            }
            else
            {
                await Wait.For(async () => (await legacyTentacleClient.ScriptService.AsyncService.GetStatusAsync(scriptStatusRequest, new(CancellationToken, null)))
                    .State == ProcessState.Complete, CancellationToken);
            }
        }

        [Test]
        [TentacleConfigurations(testNoCapabilitiesServiceVersions: true)]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_WithATentacleThatOnlySupportsV1ScriptService_TheCallIsNotRetried(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptService()
                    .RecordExceptionThrownInScriptService(out var scriptServiceExceptions)
                    .CountCallsToScriptService(out var scriptServiceCallCounts)
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder()
                        .BeforeCompleteScript(async () =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceExceptions.CompleteScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder().WithScriptBody(new ScriptBuilder().Print("hello")).Build();

            List<ProcessOutput> logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, logs, CancellationToken));

            // We Can not verify what will be in the logs because of race conditions in tentacle.
            // The last complete script which we fail might come back with the logs.

            scriptServiceExceptions.CompleteScriptLatestException.Should().NotBeNull();
            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }
    }
}
