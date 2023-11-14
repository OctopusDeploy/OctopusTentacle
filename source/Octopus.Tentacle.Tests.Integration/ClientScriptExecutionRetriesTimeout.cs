using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel or walk away (if code does not cooperate with cancellation tokens)
    /// from RPC calls when they are being retried and the rpc timeout period elapses.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientScriptExecutionRetriesTimeout : IntegrationTest
    {
        readonly TimeSpan retryIfRemainingDurationAtLeastBuffer = TimeSpan.FromSeconds(1);
        readonly TimeSpan retryBackoffBuffer = TimeSpan.FromSeconds(2);

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] {typeof(RpcCallStage)})]
        public async Task WhenRpcRetriesTimeOut_DuringGetCapabilities_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptMethodUsages)
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2>(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            // Kill the first GetCapabilities call to force the rpc call into retries
                            if (capabilitiesMethodUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException is null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder.Value.EnterKillNewAndExistingConnectionsMode();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Should not run this script"))
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;

            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            capabilitiesMethodUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).Started.Should().BeGreaterOrEqualTo(2);
            scriptMethodUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(0, "Test should not have not proceeded past GetCapabilities");

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenGetCapabilitiesFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptMethodUsages)
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2>(
                        nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        async (_,_) =>
                        {
                            await tcpConnectionUtilities.RestartTcpConnection();

                            // Sleep to make the initial RPC call take longer than the allowed retry duration
                            await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                            // Kill the first GetCapabilities call to force the rpc call into retries
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .Build();

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            capabilitiesMethodUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).Started.Should().Be(1);
            scriptMethodUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(0, "Test should not have not proceeded past GetCapabilities");

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage)})]
        public async Task WhenRpcRetriesTimeOut_DuringStartScript_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.StartScriptAsync),
                        async (_, _) =>
                        {
                            // Kill the first StartScript call to force the rpc call into retries
                            if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder.Value.EnterKillNewAndExistingConnectionsMode();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().BeGreaterOrEqualTo(2);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenStartScriptFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.StartScriptAsync),
                        async (_, _) =>
                            {
                                // Sleep to make the initial RPC call take longer than the allowed retry duration
                                await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                                // Kill the first StartScript call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage)})]
        public async Task WhenRpcRetriesTimeOut_DuringGetStatus_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.GetStatusAsync),
                        async (_, _) =>
                        {
                            // Kill the first StartScript call to force the rpc call into retries
                            if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder.Value.EnterKillNewAndExistingConnectionsMode();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }
                            }
                        })
                        .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                // Don't wait in start script as we want to tst get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();


            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterOrEqualTo(2);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenGetStatusFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.GetStatusAsync),
                        async (_, _) =>
                        {
                            // Sleep to make the initial RPC call take longer than the allowed retry duration
                            await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                            // Kill the first StartScript call to force the rpc call into retries
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Sleep(TimeSpan.FromHours(1)))
                // Don't wait in start script as we want to test get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, inMemoryLog);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] {typeof(RpcCallStage)})]
        public async Task WhenRpcRetriesTimeOut_DuringCancelScript_TheRpcCallIsCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CancelScriptAsync),
                        async (_, _) =>
                        {
                            // Kill the first StartScript call to force the rpc call into retries
                            if (recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).LastException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder.Value.EnterKillNewAndExistingConnectionsMode();
                                }
                                else
                                {
                                    // Pause the port forwarder so the next requests are in-flight when retries timeout
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse();
                                }
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                // Don't wait in start script as we want to tst get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            // Start the script which will wait for a file to exist
            var testCancellationTokenSource = new CancellationTokenSource();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(
                startScriptCommand,
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, testCancellationTokenSource.Token).Token,
                null,
                inMemoryLog);

            Func<Task> action = async () => await executeScriptTask;

            await Wait.For(() => recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Completed > 0, CancellationToken);

            // We cancel script execution via the cancellation token. This should trigger the CancelScript RPC call to be made
            testCancellationTokenSource.Cancel();

            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterOrEqualTo(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().BeGreaterOrEqualTo(2);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(clientAndTentacle.RpcRetrySettings.RetryDuration - retryIfRemainingDurationAtLeastBuffer - retryBackoffBuffer);

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndRetryFailure();
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenCancelScriptFails_AndTakesLongerThanTheRetryDuration_TheCallIsNotRetried_AndTimesOut(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var retryDuration = TimeSpan.FromSeconds(15);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(retryDuration)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CancelScriptAsync),
                        async (_, _) =>
                        {
                            // Sleep to make the initial RPC call take longer than the allowed retry duration
                            await Task.Delay(retryDuration + TimeSpan.FromSeconds(1));

                            // Kill the first StartScript call to force the rpc call into retries
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        })
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Sleep(TimeSpan.FromHours(1)))
                // Don't wait in start script as we want to test get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            // Start the script which will wait for a file to exist
            var testCancellationTokenSource = new CancellationTokenSource();

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(
                startScriptCommand,
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, testCancellationTokenSource.Token).Token,
                null,
                inMemoryLog);

            Func<Task> action = async () => await executeScriptTask;
            await Wait.For(() => recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Completed > 0, CancellationToken);

            // We cancel script execution via the cancellation token. This should trigger the CancelScript RPC call to be made
            testCancellationTokenSource.Cancel();

            await action.Should().ThrowAsync<HalibutClientException>();

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterOrEqualTo(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);

            inMemoryLog.ShouldHaveLoggedRetryFailureAndNoRetryAttempts();
        }
    }
}
