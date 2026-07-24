using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionCanRecoverFromNetworkIssues : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringStartScript_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var scriptHasStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "scripthasstarted");
            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(scriptHasStartFile)
                    .WaitForFileToExist(waitForFile)
                    .Print("hello"))
                // Configure the start script command to wait a long time, so we have plenty of time to kill the connection.
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromHours(1))
                .Build();

            var inMemoryLog = new InMemoryLog();

            var execScriptTask = Task.Run(
                async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog),
                CancellationToken);

            // Wait for the script to start.
            await Wait.For(() => File.Exists(scriptHasStartFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            // Now it has started, kill active connections killing the start script request.
            clientTentacle.PortForwarder.CloseExistingConnections();

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().BeGreaterThan(1);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeGetStatus(
                            async () =>
                            {
                                await Task.CompletedTask;

                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException is null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            var inMemoryLog = new InMemoryLog();

            var execScriptTask = Task.Run(
                async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog),
                CancellationToken);

            await Wait.For(() => recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException != null,
                TimeSpan.FromSeconds(60),
                () => throw new Exception("GetStatus did not error"),
                CancellationToken);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException.Should().NotBeNull();

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            bool completeScriptWasCalled = false;
            PortForwarder? portForwarder = null;
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                    serviceEndpoint.RetryCountLimit = 1;
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeCompleteScript(
                            async () =>
                            {

                                await Task.CompletedTask;

                                completeScriptWasCalled = true;
                                // A successfully CompleteScript call is not required for the script to be completed.
                                // So it should be the case that the tentacle can be no longer contactable at this point,
                                // yet the script execution is marked as successful.
                                portForwarder.Dispose();
                            }))
                    .Build())
                .Build(CancellationToken);
            portForwarder = clientTentacle.PortForwarder;

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().Print("hello").Sleep(TimeSpan.FromSeconds(1)))
                .Build();

            var inMemoryLog = new InMemoryLog();
            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            completeScriptWasCalled.Should().BeTrue("The tests expects that the client actually called this");

            inMemoryLog.ShouldNotHaveLoggedRetryAttemptsOrRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCancelScript_TheClientIsAbleToSuccessfullyCancelTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            using var tmp = new TemporaryDirectory();
            var scriptIsRunningFlag = Path.Combine(tmp.DirectoryPath, "scriptisrunning");

            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeGetStatus(
                            async () =>
                            {
                                await Wait.For(() => File.Exists(scriptIsRunningFlag),
                                    TimeSpan.FromSeconds(30),
                                    () => throw new Exception("Script did not start"),
                                    CancellationToken);
                                cts.Cancel();
                            })
                        .BeforeCancelScript(
                            async () =>
                            {
                                await Task.CompletedTask;

                                if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException == null)
                                {
                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .CreateFile(scriptIsRunningFlag)
                    .Sleep(TimeSpan.FromMinutes(2))
                    .Print("AllDone"))
                .Build();

            Exception? actualException = null;
            var logs = new List<ProcessOutput>();
            var inMemoryLog = new InMemoryLog();

            try
            {
                await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand,
                    onScriptStatusResponseReceived =>
                    {
                        logs.AddRange(onScriptStatusResponseReceived.Logs);
                    },
                    _ => Task.CompletedTask,
                    new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(inMemoryLog),
                    cts.Token);
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientTentacle).Build();

            actualException!.ShouldMatchExceptionContract(expectedException);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            allLogs.Should().NotContain("AllDone");

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException.Should().NotBeNull();

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetCapabilities_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var recordedUsages)
                    .DecorateCapabilitiesServiceV2With(d => d
                        .BeforeGetCapabilities(
                            async () =>
                            {
                                if (recordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException == null)
                                {
                                    await tcpConnectionUtilities.EnsureConnectionIsSetupBeforeKillingIt();

                                    responseMessageTcpKiller.KillConnectionOnNextResponse();
                                }
                            }))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().Print("hello"))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");

            recordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException.Should().NotBeNull();
            recordedUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).Started.Should().Be(2);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }
    }
}
