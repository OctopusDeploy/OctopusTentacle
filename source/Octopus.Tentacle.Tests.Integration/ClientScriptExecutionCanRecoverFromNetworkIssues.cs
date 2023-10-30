using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
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
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .Build())
                .Build(CancellationToken);

            var scriptHasStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "scripthasstarted");
            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
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
            await Wait.For(() => File.Exists(scriptHasStartFile), CancellationToken);

            // Now it has started, kill active connections killing the start script request.
            clientTentacle.PortForwarder.CloseExistingConnections();

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().BeGreaterThan(1);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetStatus_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exceptions)
                    .LogCallsToScriptServiceV2()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(async () =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceV2Exceptions.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .WaitForFileToExist(waitForFile)
                    .Print("AllDone"))
                .Build();

            var inMemoryLog = new InMemoryLog();

            var execScriptTask = Task.Run(
                async () => await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog), 
                CancellationToken);

            await Wait.For(() => scriptServiceV2Exceptions.GetStatusLatestException != null, CancellationToken);

            // Let the script finish.
            File.WriteAllText(waitForFile, "");

            var (finalResponse, logs) = await execScriptTask;

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            scriptServiceV2Exceptions.GetStatusLatestException.Should().NotBeNull();

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringCompleteScript_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            bool completeScriptWasCalled = false;
            PortForwarder? portForwarder = null;
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                    serviceEndpoint.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(10);
                    serviceEndpoint.RetryCountLimit = 1;
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeCompleteScript(async () =>
                        {
                            await Task.CompletedTask;

                            completeScriptWasCalled = true;
                            // A successfully CompleteScript call is not required for the script to be completed.
                            // So it should be the case that the tentacle can be no longer contactable at this point,
                            // yet the script execution is marked as successful.
                            portForwarder.Dispose();
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);
            portForwarder = clientTentacle.PortForwarder;

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello").Sleep(TimeSpan.FromSeconds(1)))
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

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exceptions)
                    .DecorateScriptServiceV2With(new ScriptServiceV2DecoratorBuilder()
                        .BeforeGetStatus(async () =>
                        {
                            await Task.CompletedTask;
                            Wait.For(() => File.Exists(scriptIsRunningFlag), CancellationToken).GetAwaiter().GetResult();
                            cts.Cancel();
                        })
                        .BeforeCancelScript(async () =>
                        {
                            await Task.CompletedTask;

                            if (scriptServiceV2Exceptions.CancelScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
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
                    new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog().Chain(inMemoryLog),
                    cts.Token);
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>().And.Match<Exception>(x => x.Message == "Script execution was cancelled");

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            allLogs.Should().NotContain("AllDone");
            scriptServiceV2Exceptions.CancelScriptLatestException.Should().NotBeNull();

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenANetworkFailureOccurs_DuringGetCapabilities_TheClientIsAbleToSuccessfullyCompleteTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            IAsyncClientScriptServiceV2? asyncScriptServiceV2 = null;

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToCapabilitiesServiceV2()
                    .CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceV2CallCounts)
                    .RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceV2Exceptions)
                    .DecorateCapabilitiesServiceV2With(new CapabilitiesServiceV2DecoratorBuilder()
                        .BeforeGetCapabilities(async inner =>
                        {
                            if (capabilitiesServiceV2Exceptions.GetCapabilitiesLatestException == null)
                            {
                                await asyncScriptServiceV2.EnsureTentacleIsConnectedToServer(Logger);

                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            asyncScriptServiceV2 = clientTentacle.Server.ServerHalibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(clientTentacle.ServiceEndPoint);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello"))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();
            allLogs.Should().Contain("hello");
            capabilitiesServiceV2Exceptions.GetCapabilitiesLatestException.Should().NotBeNull();
            capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountStarted.Should().Be(2);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }
    }
}
