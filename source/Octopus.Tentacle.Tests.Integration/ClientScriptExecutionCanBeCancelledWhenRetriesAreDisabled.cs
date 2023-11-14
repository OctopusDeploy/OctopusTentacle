using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.Tentacle.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel the ExecuteScript operation when using Tentacle Client with RPC retries disabled.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientScriptExecutionCanBeCancelledWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage) })]
        public async Task DuringGetCapabilities_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            var hasPausedOrStoppedPortForwarder = false;
            var ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point => point.TryAndConnectForALongTime())
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var scriptMethodUsages)
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2, object, CapabilitiesResponseV2>(
                        nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        async (_, _) =>
                        {
                            ensureCancellationOccursDuringAnRpcCall.Release();

                            if (!hasPausedOrStoppedPortForwarder)
                            {
                                hasPausedOrStoppedPortForwarder = true;
                                await tcpConnectionUtilities.RestartTcpConnection();

                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }
                        }, async (_, _) =>
                        {
                            await ensureCancellationOccursDuringAnRpcCall.WaitAsync(CancellationToken);
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b
                    .Print("Should not run this script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeTaskOrOperationCancelledException();

            // If the rpc call could be cancelled then the correct error was recorded
            var latestException = capabilitiesMethodUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).LastException;
            switch (rpcCallStage)
            {
                case RpcCallStage.Connecting:
                    latestException.Should().BeTaskOrOperationCancelledException().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case RpcCallStage.InFlight:
                    latestException?.Should().BeOfType<HalibutClientException>();
                    break;
            }

            // If the rpc call could be cancelled we cancelled quickly
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(12));
            }

            // Or if the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            capabilitiesMethodUsages.For(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync)).Started.Should().Be(1);

            scriptMethodUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(0, "Should not have proceeded past GetCapabilities");
            scriptMethodUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0, "Should not have tried to call CancelScript");
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage) })]
        public async Task DuringStartScript_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            var expectedFlow = rpcCallStage switch
            {
                RpcCallStage.Connecting => ExpectedFlow.CancelRpcAndExitImmediately,
                RpcCallStage.InFlight => ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript,
                _ => throw new ArgumentOutOfRangeException()
            };

            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            TimeSpan? lastCallDuration = null;
            var restartedPortForwarderForCancel = false;
            var hasPausedOrStoppedPortForwarder = false;
            SemaphoreSlim ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);
            var timer = new Stopwatch();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point => point.TryAndConnectForALongTime())
                .WithRetriesDisabled()
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
                            ensureCancellationOccursDuringAnRpcCall.Release();

                            if (!hasPausedOrStoppedPortForwarder)
                            {
                                hasPausedOrStoppedPortForwarder = true;
                                await tcpConnectionUtilities.RestartTcpConnection();
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }

                            timer.Restart();
                        },
                        async (_, _) =>
                        {
                            await Task.CompletedTask;

                            timer.Stop();
                            lastCallDuration = timer.Elapsed;
                        })
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CancelScriptAsync),
                        async (_, _) =>
                        {
                            await Task.CompletedTask;

                            if (!restartedPortForwarderForCancel)
                            {
                                restartedPortForwarderForCancel = true;
                                UnPauseOrRestartPortForwarder(tentacleConfigurationTestCase.TentacleType, rpcCallStage, portForwarder);
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeTaskOrOperationCancelledException();

            // If the rpc call could be cancelled then the correct error was recorded
            var latestException = recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).LastException;
            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcAndExitImmediately:
                    latestException.Should().BeTaskOrOperationCancelledException().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    latestException?.Should().BeOfType<HalibutClientException>();
                    break;
                default:
                    throw new NotSupportedException();
            }

            // If the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0, "Test should not have not proceeded past StartScript before being Cancelled");

            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcAndExitImmediately:
                    recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
                    recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(0);
                    break;
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().BeGreaterOrEqualTo(1);
                    recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage) })]
        public async Task DuringGetStatus_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            // ARRANGE
            var expectedFlow = rpcCallStage switch
            {
                RpcCallStage.Connecting => ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript,
                RpcCallStage.InFlight => ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript,
                _ => throw new ArgumentOutOfRangeException()
            };
            var rpcCallHasStarted = new Reference<bool>(false);
            TimeSpan? lastCallDuration = null;
            var restartedPortForwarderForCancel = false;
            var hasPausedOrStoppedPortForwarder = false;
            SemaphoreSlim ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);
            var timer = new Stopwatch();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point => point.TryAndConnectForALongTime())
                .WithRetriesDisabled()
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
                            ensureCancellationOccursDuringAnRpcCall.Release();

                            if (!hasPausedOrStoppedPortForwarder)
                            {
                                hasPausedOrStoppedPortForwarder = true;
                                await tcpConnectionUtilities.RestartTcpConnection();
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }

                            timer.Restart();
                        },
                        async (_, _) =>
                        {
                            timer.Stop();
                            lastCallDuration = timer.Elapsed;

                            await ensureCancellationOccursDuringAnRpcCall.WaitAsync(CancellationToken);
                        })
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CancelScriptAsync),
                        async (_, _) =>
                        {
                            await Task.CompletedTask;

                            if (!restartedPortForwarderForCancel)
                            {
                                restartedPortForwarderForCancel = true;
                                UnPauseOrRestartPortForwarder(tentacleConfigurationTestCase.TentacleType, rpcCallStage, portForwarder);
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);

            // ASSERT
            // The ExecuteScript operation threw an OperationCancelledException
            actualException.Should().BeTaskOrOperationCancelledException();

            // If the rpc call could be cancelled then the correct error was recorded
            var latestException = recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException;
            switch (expectedFlow)
            {
                case ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript:
                    latestException.Should().BeTaskOrOperationCancelledException().And.NotBeOfType<OperationAbandonedException>();
                    break;
                case ExpectedFlow.AbandonRpcThenCancelScriptThenCompleteScript:
                    latestException?.Should().BeOfType<HalibutClientException>();
                    break;
                default:
                    throw new NotSupportedException();
            }

            // If the rpc call could be cancelled we cancelled quickly
            if (expectedFlow == ExpectedFlow.CancelRpcThenCancelScriptThenCompleteScript)
            {
                // This last call includes the time it takes to cancel and hence is why I kept pushing it up
                lastCallDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(12 + 2)); // + 2 seconds for some error of margin
            }

            // Or if the rpc call could not be cancelled it cancelled fairly quickly e.g. we are not waiting for an rpc call to timeout
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(1);

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().BeGreaterOrEqualTo(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage) })]
        public async Task DuringCompleteScript_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            var hasPausedOrStoppedPortForwarder = false;

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point => point.TryAndConnectForALongTime())
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync),
                        async (_, _) =>
                        {
                            if (!hasPausedOrStoppedPortForwarder)
                            {
                                hasPausedOrStoppedPortForwarder = true;
                                await tcpConnectionUtilities.RestartTcpConnection();
                                PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            clientAndTentacle.TentacleClient.OnCancellationAbandonCompleteScriptAfter = TimeSpan.FromSeconds(20);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromSeconds(5)))
                .Build();

            // ACT
            var (responseAndLogs, _, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, new SemaphoreSlim(Int32.MaxValue, Int32.MaxValue));

            // ASSERT
            // Halibut Errors were recorded on CompleteScript
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).LastException?.Should().Match<Exception>(x => x is HalibutClientException || x is OperationCanceledException || x is TaskCanceledException); // Complete Script was cancelled quickly
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

            // The expected RPC calls were made - this also verifies the integrity of the test
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterThanOrEqualTo(1);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().BeGreaterOrEqualTo(1);
        }

        private void PauseOrStopPortForwarder(RpcCallStage rpcCallStage, PortForwarder portForwarder, IResponseMessageTcpKiller responseMessageTcpKiller, Reference<bool> rpcCallHasStarted)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                Logger.Information("Killing the port forwarder so the next RPCs are in the connecting state when being cancelled");
                //portForwarder.Stop();
                portForwarder.EnterKillNewAndExistingConnectionsMode();
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
                //portForwarder.Value.Start();
                portForwarder.Value.ReturnToNormalMode();
            }
            else if (tentacleType == TentacleType.Polling)
            {
                Logger.Information("UnPausing the PortForwarder as we paused the connections which means Polling will be stalled");
                portForwarder.Value.UnPauseExistingConnections();
                portForwarder.Value.CloseExistingConnections();
            }
        }

        private async Task<(ScriptExecutionResult response, Exception? actualException, TimeSpan cancellationDuration)> ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(
            ClientAndTentacle clientAndTentacle,
            StartScriptCommandV3Alpha startScriptCommand,
            Reference<bool> rpcCallHasStarted,
            SemaphoreSlim whenTheRequestCanBeCancelled)
        {
            Logger.Information("Start of ExecuteScriptThenCancel");
            var cancelExecutionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(
                startScriptCommand,
                cancelExecutionCancellationTokenSource.Token);

            Logger.Information("Create action");
            Func<Task<(ScriptExecutionResult, List<ProcessOutput>)>> action = async () => await executeScriptTask;

            Logger.Information("Waiting for the RPC Call to start");
            await Wait.For(() => rpcCallHasStarted.Value, CancellationToken);
            Logger.Information("RPC Call has start");

            await Task.Delay(TimeSpan.FromSeconds(6), CancellationToken);

            var cancellationDuration = new Stopwatch();
            await whenTheRequestCanBeCancelled.WithLockAsync(() =>
            {
                Logger.Information("Cancelling ExecuteScript");
                cancelExecutionCancellationTokenSource.Cancel();
                cancellationDuration.Start();
            }, CancellationToken);

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

        public enum ExpectedFlow
        {
            CancelRpcAndExitImmediately,
            CancelRpcThenCancelScriptThenCompleteScript,
            AbandonRpcThenCancelScriptThenCompleteScript
        }
    }
}