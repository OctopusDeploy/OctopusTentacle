using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel the ExecuteScript operation when using Tentacle Client.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientScriptExecutionCanBeCancelled : IntegrationTest
    {
        [Test]
        [RetryInconclusive(5)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.FirstCall)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.FirstCall)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.FirstCall)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.FirstCall)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.RetryingCall)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.RetryingCall)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.RetryingCall)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.RetryingCall)]
        public async Task DuringGetCapabilities_ScriptExecutionCanBeCancelled(TentacleType tentacleType, RpcCallStage rpcCallStage, RpcCall rpcCall)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogAndCountAllCalls(out var capabilitiesServiceV2CallCounts, out _, out var scriptServiceV2CallCounts, out _)
                    .RecordAllExceptions(out var capabilityServiceV2Exceptions, out _, out _, out _)
                    .DecorateCapabilitiesServiceV2With(d => d
                        .BeforeGetCapabilities((service) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);

                            if (rpcCall == RpcCall.RetryingCall && capabilityServiceV2Exceptions.GetCapabilitiesLatestException == null)
                            {
                                // Kill the first GetCapabilities call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Should not run this script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted);

            if (AssertInconclusiveIfHalibutDequeueToClosedConnection(tentacleType, rpcCallStage, rpcCall, () => capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountStarted)) return;
            if (AssertInconclusiveIfListeningClientDidNotCancelConnectingCallButAbandoned(tentacleType, rpcCallStage, actualException)) return;

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeOfType<OperationCanceledException>();

            // If the rpc call could be cancelled then the correct error was recorded
            switch (rpcCallStage)
            {
                case RpcCallStage.Connecting:
                    capabilityServiceV2Exceptions.GetCapabilitiesLatestException.Should().BeOfType<OperationCanceledException>().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case RpcCallStage.InFlight:
                    capabilityServiceV2Exceptions.GetCapabilitiesLatestException?.Should().BeOfType<HalibutClientException>();
                    break;
            }

            // If the rpc call could be cancelled we cancelled quickly
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(rpcCall == RpcCall.RetryingCall ? 6 : 12));
            }
            // Or if the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountStarted.Should().Be(rpcCall == RpcCall.RetryingCall ? 2 : 1);
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(0, "Should not have proceeded past GetCapabilities");
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0, "Should not have tried to call CancelScript");
        }

        private static bool AssertInconclusiveIfListeningClientDidNotCancelConnectingCallButAbandoned(TentacleType tentacleType, RpcCallStage rpcCallStage, Exception? actualException)
        {
            if (tentacleType == TentacleType.Listening && rpcCallStage == RpcCallStage.Connecting && actualException is not OperationCanceledException)
            {
                Assert.Inconclusive("Listening Client is not cancelling the RPC call during the connecting phase reliably. This results in the RPC call being abandoned rather than cancelled.");
                return true;
            }

            return false;
        }

        [Test]
        [RetryInconclusive(5)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.FirstCall, ExpectedFlow.CancelRpcAndExitImmediately)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.FirstCall, ExpectedFlow.CancelRpcAndExitImmediately)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.RetryingCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.RetryingCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.FirstCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.FirstCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.RetryingCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.RetryingCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        public async Task DuringStartScript_ScriptExecutionCanBeCancelled(TentacleType tentacleType, RpcCallStage rpcCallStage, RpcCall rpcCall, ExpectedFlow expectedFlow)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            TimeSpan? lastCallDuration = null;
            var restartedPortForwarderForCancel = false;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogAndCountAllCalls(out _, out _, out var scriptServiceV2CallCounts, out _)
                    .RecordAllExceptions(out _, out _, out var scriptServiceV2Exceptions, out _)
                    .DecorateScriptServiceV2With(d => d
                        .DecorateStartScriptWith((service, command) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);

                            if (rpcCall == RpcCall.RetryingCall && scriptServiceV2Exceptions.StartScriptLatestException == null)
                            {
                                // Kill the first StartScript call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                            }

                            var timer = Stopwatch.StartNew();
                            try
                            {
                                return service.StartScript(command);
                            }
                            finally
                            {
                                timer.Stop();
                                lastCallDuration = timer.Elapsed;
                            }
                        })
                        .BeforeCancelScript(() =>
                        {
                            if (!restartedPortForwarderForCancel)
                            {
                                restartedPortForwarderForCancel = true;
                                UnPauseOrRestartPortForwarder(tentacleType, rpcCallStage, portForwarder);
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted);

            // Flakey factors make some tests runs invalid - To be improved where possible
            if (AssertInconclusiveIfPortforwarderCannotBeRestarted(actualException)) return;
            if (AssertInconclusiveIfHalibutDequeueToClosedConnection(tentacleType, rpcCallStage, rpcCall, () => scriptServiceV2CallCounts.StartScriptCallCountStarted)) return;
            if (AssertInconclusiveIfListeningClientDidNotCancelConnectingCallButAbandoned(tentacleType, rpcCallStage, actualException)) return;

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeOfType<OperationCanceledException>();

            // If the rpc call could be cancelled then the correct error was recorded
            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcAndExitImmediately:
                case ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript:
                    scriptServiceV2Exceptions.StartScriptLatestException.Should().BeOfType<OperationCanceledException>().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    scriptServiceV2Exceptions.StartScriptLatestException?.Should().BeOfType<HalibutClientException>();
                    break;
                default:
                    throw new NotSupportedException();
            }

            // If the rpc call could be cancelled we cancelled quickly
            if (expectedFlow == ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)
            {
                lastCallDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(rpcCall == RpcCall.RetryingCall ? 6 : 12));
            }
            // Or if the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(rpcCall == RpcCall.RetryingCall ? 2 : 1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().Be(0, "Test should not have not proceeded past StartScript before being Cancelled");

            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcAndExitImmediately:
                    scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
                    scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(0);
                    break;
                case ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript:
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().BeGreaterOrEqualTo(1);
                    scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        [Test]
        [RetryInconclusive(5)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.FirstCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.FirstCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, RpcCall.RetryingCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, RpcCall.RetryingCall, ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.FirstCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.FirstCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, RpcCall.RetryingCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, RpcCall.RetryingCall, ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript)]
        public async Task DuringGetStatus_ScriptExecutionCanBeCancelled(TentacleType tentacleType, RpcCallStage rpcCallStage, RpcCall rpcCall, ExpectedFlow expectedFlow)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            TimeSpan? lastCallDuration = null;
            var restartedPortForwarderForCancel = false;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogAndCountAllCalls(out _, out _, out var scriptServiceV2CallCounts, out _)
                    .RecordAllExceptions(out _, out _, out var scriptServiceV2Exceptions, out _)
                    .DecorateScriptServiceV2With(d => d
                        .DecorateGetStatusWith((service, request) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);

                            if (rpcCall == RpcCall.RetryingCall && scriptServiceV2Exceptions.GetStatusLatestException == null)
                            {
                                // Kill the first StartScript call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                            }

                            var timer = Stopwatch.StartNew();
                            try
                            {
                                return service.GetStatus(request);
                            }
                            finally
                            {
                                timer.Stop();
                                lastCallDuration = timer.Elapsed;
                            }
                        })
                        .BeforeCancelScript(() =>
                        {
                            if (!restartedPortForwarderForCancel)
                            {
                                restartedPortForwarderForCancel = true;
                                UnPauseOrRestartPortForwarder(tentacleType, rpcCallStage, portForwarder);
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted);

            // Flakey factors make some tests runs invalid - To be improved where possible
            if (AssertInconclusiveIfPortforwarderCannotBeRestarted(actualException)) return;
            if (AssertInconclusiveIfHalibutDequeueToClosedConnection(tentacleType, rpcCallStage, rpcCall, () => scriptServiceV2CallCounts.GetStatusCallCountStarted)) return;
            if (AssertInconclusiveIfListeningClientDidNotCancelConnectingCallButAbandoned(tentacleType, rpcCallStage, actualException)) return;

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeOfType<OperationCanceledException>();

            // If the rpc call could be cancelled then the correct error was recorded
            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript:
                    scriptServiceV2Exceptions.GetStatusLatestException.Should().BeOfType<OperationCanceledException>().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    scriptServiceV2Exceptions.GetStatusLatestException?.Should().BeOfType<HalibutClientException>();
                    break;
                default:
                    throw new NotSupportedException();
            }

            // If the rpc call could be cancelled we cancelled quickly
            if (expectedFlow == ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)
            {
                lastCallDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(rpcCall == RpcCall.RetryingCall ? 6 : 12));
            }
            // Or if the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().Be(rpcCall == RpcCall.RetryingCall ? 2 : 1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().BeGreaterOrEqualTo(1);
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }

        [Test]
        [RetryInconclusive(5)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting)]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight)]
        public async Task DuringCompleteScript_ScriptExecutionCanBeCancelled(TentacleType tentacleType, RpcCallStage rpcCallStage)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogAndCountAllCalls(out _, out _, out var scriptServiceV2CallCounts, out _)
                    .RecordAllExceptions(out _, out _, out var scriptServiceV2Exceptions, out _)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeCompleteScript((service, _) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);
                            PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);

            clientAndTentacle.TentacleClient.OnCancellationAbandonCompleteScriptAfter = TimeSpan.FromSeconds(20);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromSeconds(5)))
                .Build();

            // ACT
            var (responseAndLogs, _, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted);

            // Flakey factors make some tests runs invalid - To be improved where possible
            if (AssertInconclusiveIfHalibutDequeueToClosedConnection(tentacleType, rpcCallStage, RpcCall.FirstCall, () => scriptServiceV2CallCounts.CompleteScriptCallCountStarted)) return;

            // ASSERT
            // Halibut Errors were recorded on CompleteScript
            scriptServiceV2Exceptions.CompleteScriptLatestException?.Should().Match<Exception>(x => x.GetType() == typeof(HalibutClientException) || x.GetType() == typeof(OperationCanceledException));

            // Complete Script was cancelled quickly
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThanOrEqualTo(1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }

        private void PauseOrStopPortForwarder(RpcCallStage rpcCallStage, PortForwarder portForwarder, IResponseMessageTcpKiller responseMessageTcpKiller, Reference<bool> rpcCallHasStarted)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                Logger.Information("Killing the port forwarder so the next RPCs are in the connecting state when being cancelled");
                portForwarder.Stop();
                rpcCallHasStarted.Value = true;
            }
            else
            {
                Logger.Information("Will Pause the port forwarder on next response so the next RPC is in-flight when being cancelled");
                responseMessageTcpKiller.PauseConnectionOnNextResponse(() => rpcCallHasStarted.Value = true);
            }
        }

        private void UnPauseOrRestartPortForwarder(TentacleType tentacleType, RpcCallStage rpcCallStage, Reference<PortForwarder> portForwarder)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                Logger.Information("Starting the PortForwarder as we stopped it to get the StartScript RPC call in the Connecting state");
                portForwarder.Value.Start();
            }
            else if (tentacleType == TentacleType.Polling)
            {
                Logger.Information("UnPausing the PortForwarder as we paused the connections which means Polling will be stalled");
                portForwarder.Value.UnPauseExistingConnections();
                portForwarder.Value.CloseExistingConnections();
            }
        }

        private async Task<(ScriptExecutionResult response, Exception? actualException, TimeSpan cancellationDuration)> ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(ClientAndTentacle clientAndTentacle, StartScriptCommandV2 startScriptCommand, Reference<bool> rpcCallHasStarted)
        {
            var cancelExecutionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(
                startScriptCommand,
                cancelExecutionCancellationTokenSource.Token);

            Func<Task<(ScriptExecutionResult, List<ProcessOutput>)>> action = async () => await executeScriptTask;

            Logger.Information("Waiting for the RPC Call to start");
            await Wait.For(() => rpcCallHasStarted.Value, CancellationToken);
            Logger.Information("RPC Call has start");

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);

            Logger.Information("Cancelling ExecuteScript");
            var cancellationDuration = Stopwatch.StartNew();
            cancelExecutionCancellationTokenSource.Cancel();

            Exception? actualException = null;
            (ScriptExecutionResult Response, List<ProcessOutput> Logs)? responseAndLogs = null;
            try
            {
                responseAndLogs = await action();
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            cancellationDuration.Stop();
            return (responseAndLogs?.Response, actualException, cancellationDuration.Elapsed);
        }

        private static bool AssertInconclusiveIfHalibutDequeueToClosedConnection(TentacleType tentacleType, RpcCallStage rpcCallStage, RpcCall rpcCall, Func<long> actualCallCounts)
        {
            if (tentacleType == TentacleType.Polling &&
                rpcCallStage == RpcCallStage.Connecting &&
                actualCallCounts() > (rpcCall == RpcCall.FirstCall ? 1 : 2))
            {
                // Known bug
                Assert.Inconclusive("The request was queued for the Polling endpoint and immediately returned an error due to a previously connected tentacle that was polling for a request disconnected before a request or null was returned");
                return true;
            }

            return false;
        }

        private static bool AssertInconclusiveIfPortforwarderCannotBeRestarted(Exception? actualException)
        {
            if (actualException is SocketException s && s.Message.Contains("Only one usage of each socket address (protocol/network address/port) is normally permitted"))
            {
                Assert.Inconclusive("Could not restart the port forwarder. Something is hanging onto the port");
                return true;
            }

            return false;
        }

        public enum ExpectedFlow
        {
            CancelRpcAndExitImmediately,
            CancelRpcThenCancelScriptThenCompleteScript,
            AbandonRpcThenCancelScriptThenCompleteScript
        }
    }
}