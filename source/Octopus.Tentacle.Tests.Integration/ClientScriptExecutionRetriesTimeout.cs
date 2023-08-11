using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
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
        [Test]
        public async Task WhenRpcRetriesTimeOut_DuringGetCapabilities_TheRpcCallIsCancelled([Values]TentacleType tentacleType, [Values] RpcCallStage rpcCallStage, [Values] SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            IClientScriptServiceV2? scriptServiceV2 = null;
            IAsyncClientScriptServiceV2? asyncScriptServiceV2 = null;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToCapabilitiesServiceV2()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceCallCounts)
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceV2Exception)
                    .DecorateCapabilitiesServiceV2With(d => d
                        .BeforeGetCapabilities(async (service) =>
                        {
                            await syncOrAsyncHalibut.WhenSync(() => scriptServiceV2.EnsureTentacleIsConnectedToServer(Logger))
                                .WhenAsync(async () => await asyncScriptServiceV2.EnsureTentacleIsConnectedToServer(Logger));

                            // Kill the first GetCapabilities call to force the rpc call into retries
                            if (capabilitiesServiceV2Exception.GetCapabilitiesLatestException == null)
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
                    .Build())
                .Build(CancellationToken);

            syncOrAsyncHalibut.WhenSync(() => scriptServiceV2 = clientAndTentacle.Server.ServerHalibutRuntime.CreateClient<IScriptServiceV2, IClientScriptServiceV2>(clientAndTentacle.ServiceEndPoint))
                .IgnoreResult()
                .WhenAsync(() => asyncScriptServiceV2 = clientAndTentacle.Server.ServerHalibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(clientAndTentacle.ServiceEndPoint));

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Should not run this script"))
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
            Func<Task> action = async () => await executeScriptTask;

            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            capabilitiesServiceCallCounts.GetCapabilitiesCallCountStarted.Should().BeGreaterOrEqualTo(2);
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(0, "Test should not have not proceeded past GetCapabilities");

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }

        [Test]
        public async Task WhenRpcRetriesTimeOut_DuringStartScript_TheRpcCallIsCancelled([Values] TentacleType tentacleType, [Values] RpcCallStage rpcCallStage, [Values] SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exception)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeStartScript(async (service, _) =>
                        {
                            // Kill the first StartScript call to force the rpc call into retries
                            if (scriptServiceV2Exception.StartScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await service.EnsureTentacleIsConnectedToServer(Logger);

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
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            scriptServiceCallCounts.StartScriptCallCountStarted.Should().BeGreaterOrEqualTo(2);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }

        [Test]
        public async Task WhenRpcRetriesTimeOut_DuringGetStatus_TheRpcCallIsCancelled([Values] TentacleType tentacleType, [Values] RpcCallStage rpcCallStage, [Values] SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exception)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeGetStatus(async (service, _) =>
                        {
                            // Kill the first GetStatus call to force the rpc call into retries
                            if (scriptServiceV2Exception.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await service.EnsureTentacleIsConnectedToServer(Logger);

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
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                // Don't wait in start script as we want to tst get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().BeGreaterOrEqualTo(2);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }

        [Test]
        public async Task WhenRpcRetriesTimeOut_DuringCancelScript_TheRpcCallIsCancelled([Values] TentacleType tentacleType, [Values] RpcCallStage rpcCallStage, [Values] SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(15))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exception)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeCancelScript(async (service, _) =>
                        {
                            // Kill the first CancelScript call to force the rpc call into retries
                            if (scriptServiceV2Exception.CancelScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                await service.EnsureTentacleIsConnectedToServer(Logger);

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
                    .Build())
                .Build(CancellationToken);

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
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, testCancellationTokenSource.Token).Token);

            Func<Task> action = async () => await executeScriptTask;

            await Wait.For(() => scriptServiceCallCounts.GetStatusCallCountCompleted > 0, CancellationToken);

            // We cancel script execution via the cancellation token. This should trigger the CancelScript RPC call to be made
            testCancellationTokenSource.Cancel();

            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().BeGreaterOrEqualTo(1);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().BeGreaterOrEqualTo(2);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(14));
        }
    }
}