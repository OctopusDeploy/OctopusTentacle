using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;
using Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.Tentacle.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel the ExecuteScript operation when using Tentacle Client with RPC retries enabled.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientScriptExecutionCanBeCancelledWhenRetriesAreEnabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCallStage), typeof(RpcCall) })]
        public async Task DuringGetCapabilities_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCallStage rpcCallStage, RpcCall rpcCall)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            var hasPausedOrStoppedPortForwarder = false;
            var ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point =>
                {
                    if (rpcCall == RpcCall.FirstCall) point.TryAndConnectForALongTime();
                })
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var recordedUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptMethodUsages)
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2, object, CapabilitiesResponseV2>(
                        nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        async (_, _) =>
                        {
                            if (rpcCall == RpcCall.RetryingCall &&
                                recordedUsages.ForGetCapabilitiesAsync().LastException == null)
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();

                                // Kill the first GetCapabilities call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else if (!hasPausedOrStoppedPortForwarder)
                            {
                                hasPausedOrStoppedPortForwarder = true;
                                await tcpConnectionUtilities.RestartTcpConnection();

                                await PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting && tentacleConfigurationTestCase.TentacleType == TentacleType.Polling)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }

                            ensureCancellationOccursDuringAnRpcCall.Release();
                        }, async (_, _) =>
                        {
                            await ensureCancellationOccursDuringAnRpcCall.WaitAsync(CancellationToken);
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b
                    .Print("Should not run this script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);

            // ASSERT
            // Assert that the cancellation was performed at the correct state e.g. Connecting or Transferring
            var latestException = recordedUsages.ForGetCapabilitiesAsync().LastException;
            if (tentacleConfigurationTestCase.TentacleType == TentacleType.Listening && rpcCallStage == RpcCallStage.Connecting && latestException is HalibutClientException)
            {
                Assert.Inconclusive("This test is very fragile and often it will often cancel when the client is not in a wait trying to connect but instead gets error responses from the proxy. " +
                    "This results in a halibut client exception being returned rather than a request cancelled error being returned and is not testing the intended scenario");
            }
            
            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
            
            actualException.ShouldMatchExceptionContract(expectedException);

            latestException.Should().BeRequestCancelledException(rpcCallStage);
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10), "The RPC call should have been cancelled quickly");

            if (rpcCall == RpcCall.FirstCall)
            {
                recordedUsages.ForGetCapabilitiesAsync().Started.Should().Be(1);
            }
            else
            {
                recordedUsages.ForGetCapabilitiesAsync().Started.Should().BeGreaterOrEqualTo(2);
            }
            scriptMethodUsages.ForStartScriptAsync().Started.Should().Be(0, "Should not have proceeded past GetCapabilities");
            scriptMethodUsages.ForCancelScriptAsync().Started.Should().Be(0, "Should not have tried to call CancelScript");
            scriptMethodUsages.ForCompleteScriptAsync().Started.Should().Be(0, "Should not have tried to call CompleteScript");
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCall), typeof(RpcCallStage) })]
        public async Task DuringStartScript_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCall rpcCall, RpcCallStage rpcCallStage)
        {
            var rpcCallHasStarted = new Reference<bool>(false);
            var restartedPortForwarderForCancel = false;
            var hasPausedOrStoppedPortForwarder = false;
            var ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point =>
                {
                    if (rpcCall == RpcCall.FirstCall) point.TryAndConnectForALongTime();
                })
                .WithRetryDuration(TimeSpan.FromHours(1))
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithPortForwarder(out var portForwarder)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .HookServiceMethod(tentacleConfigurationTestCase,
                        nameof(IAsyncClientScriptServiceV2.StartScriptAsync),
                        preInvocation: async (_, _) =>
                        {
                            if (rpcCall == RpcCall.RetryingCall && recordedUsages.ForStartScriptAsync().LastException is null)
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();
                                // Kill the first StartScript call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (!hasPausedOrStoppedPortForwarder)
                                {
                                    hasPausedOrStoppedPortForwarder = true;
                                    await tcpConnectionUtilities.RestartTcpConnection();
                                    await PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                    if (rpcCallStage == RpcCallStage.Connecting)
                                    {
                                        await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                    }
                                }
                            }

                            ensureCancellationOccursDuringAnRpcCall.Release();
                        },
                        postInvocation: async (_, _) =>
                        {
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

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);
            
            // ASSERT
            // Assert that the cancellation was performed at the correct state e.g. Connecting or Transferring
            var latestException = recordedUsages.ForStartScriptAsync().LastException;
            if (tentacleConfigurationTestCase.TentacleType == TentacleType.Listening && rpcCallStage == RpcCallStage.Connecting && latestException is HalibutClientException)
            {
                Assert.Inconclusive("This test is very fragile and often it will often cancel when the client is not in a wait trying to connect but instead gets error responses from the proxy. " +
                    "This results in a halibut client exception being returned rather than a request cancelled error being returned and is not testing the intended scenario");
            }
            latestException.Should().BeRequestCancelledException(rpcCallStage);

            if (rpcCall == RpcCall.FirstCall && rpcCallStage == RpcCallStage.Connecting)
            {
                var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
            
                actualException.ShouldMatchExceptionContract(expectedException);

                // We should have cancelled the RPC call quickly
                cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(15));

                recordedUsages.ForStartScriptAsync().Started.Should().Be(1);
                recordedUsages.ForGetStatusAsync().Started.Should().Be(0);
                recordedUsages.ForCancelScriptAsync().Started.Should().Be(0);
                recordedUsages.ForCompleteScriptAsync().Started.Should().Be(0);
            }
            else if((rpcCall == RpcCall.RetryingCall && rpcCallStage == RpcCallStage.Connecting) || rpcCallStage == RpcCallStage.InFlight) 
            {
                // Assert that script execution was cancelled
                actualException.Should().BeScriptExecutionCancelledException();

                var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
            
                actualException.ShouldMatchExceptionContract(expectedException);


                // Assert the CancelScript and CompleteScript flow happened fairly quickly
                cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));

                if (rpcCall == RpcCall.FirstCall)
                {
                    recordedUsages.ForStartScriptAsync().Started.Should().Be(1);
                }
                else
                {
                    recordedUsages.ForStartScriptAsync().Started.Should().BeGreaterOrEqualTo(2);
                }
                recordedUsages.ForGetStatusAsync().Started.Should().Be(0);
                recordedUsages.ForCancelScriptAsync().Started.Should().BeGreaterOrEqualTo(1);
                recordedUsages.ForCompleteScriptAsync().Started.Should().Be(1);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        [Test]
        [TentacleConfigurations(additionalParameterTypes: new object[] { typeof(RpcCall), typeof(RpcCallStage) })]
        public async Task DuringGetStatus_ScriptExecutionCanBeCancelled(TentacleConfigurationTestCase tentacleConfigurationTestCase, RpcCall rpcCall, RpcCallStage rpcCallStage)
        {
            // ARRANGE
            var rpcCallHasStarted = new Reference<bool>(false);
            var restartedPortForwarderForCancel = false;
            var hasPausedOrStoppedPortForwarder = false;
            var ensureCancellationOccursDuringAnRpcCall = new SemaphoreSlim(0, 1);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPendingRequestQueueFactory(new CancellationObservingPendingRequestQueueFactory()) // Partially works around disconnected polling tentacles take work from the queue
                .WithServiceEndpointModifier(point =>
                {
                    if (rpcCall == RpcCall.FirstCall) point.TryAndConnectForALongTime();
                })
                .WithRetryDuration(TimeSpan.FromHours(1))
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
                            if (rpcCall == RpcCall.RetryingCall &&
                                recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).LastException is null)
                            {
                                await tcpConnectionUtilities.RestartTcpConnection();
                                // Kill the first StartScript call to force the rpc call into retries
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (!hasPausedOrStoppedPortForwarder)
                                {
                                    hasPausedOrStoppedPortForwarder = true;
                                    await tcpConnectionUtilities.RestartTcpConnection();
                                    await PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                    if (rpcCallStage == RpcCallStage.Connecting)
                                    {
                                        await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                    }
                                }
                            }

                            ensureCancellationOccursDuringAnRpcCall.Release();
                        },
                        async (_, _) =>
                        {
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

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromHours(1)))
                .Build();

            // ACT
            var (_, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, ensureCancellationOccursDuringAnRpcCall);

            // ASSERT
            // Assert that the cancellation was performed at the correct state e.g. Connecting or Transferring
            var latestException = recordedUsages.ForGetStatusAsync().LastException;
            if (tentacleConfigurationTestCase.TentacleType == TentacleType.Listening && rpcCallStage == RpcCallStage.Connecting && latestException is HalibutClientException)
            {
                Assert.Inconclusive("This test is very fragile and often it will often cancel when the client is not in a wait trying to connect but instead gets error responses from the proxy. " +
                    "This results in a halibut client exception being returned rather than a request cancelled error being returned and is not testing the intended scenario");
            }

            latestException.Should().BeRequestCancelledException(rpcCallStage);
            
            // Assert that script execution was cancelled
            actualException.Should().BeScriptExecutionCancelledException();

            var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
            
            actualException.ShouldMatchExceptionContract(expectedException);


            // Script Execution should cancel quickly
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));
            
            recordedUsages.ForStartScriptAsync().Started.Should().Be(1);
            if (rpcCall == RpcCall.FirstCall)
            {
                recordedUsages.ForGetStatusAsync().Started.Should().Be(1);
            }
            else
            {
                recordedUsages.ForGetStatusAsync().Started.Should().BeGreaterOrEqualTo(2);
            }
            recordedUsages.ForCancelScriptAsync().Started.Should().BeGreaterOrEqualTo(1);
            recordedUsages.ForCompleteScriptAsync().Started.Should().Be(1);
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
                .WithRetryDuration(TimeSpan.FromHours(1))
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
                                await PauseOrStopPortForwarder(rpcCallStage, portForwarder.Value, responseMessageTcpKiller, rpcCallHasStarted);
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    await tcpConnectionUtilities.EnsurePollingQueueWontSendMessageToDisconnectedTentacles();
                                }
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            clientAndTentacle.TentacleClient.OnCancellationAbandonCompleteScriptAfter = TimeSpan.FromSeconds(20);

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b
                    .Print("The script")
                    .Sleep(TimeSpan.FromSeconds(5)))
                .Build();

            // ACT
            var (responseAndLogs, actualException, cancellationDuration) = await ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(clientAndTentacle, startScriptCommand, rpcCallHasStarted, new SemaphoreSlim(int.MaxValue, int.MaxValue));

            // ASSERT
            
            // Assert that the cancellation was performed at the correct state e.g. Connecting or Transferring
            var latestException = recordedUsages.ForCompleteScriptAsync().LastException;
            if (tentacleConfigurationTestCase.TentacleType == TentacleType.Listening && rpcCallStage == RpcCallStage.Connecting && latestException is HalibutClientException)
            {
                Assert.Inconclusive("This test is very fragile and often it will often cancel when the client is not in a wait trying to connect but instead gets error responses from the proxy. " +
                    "This results in a halibut client exception being returned rather than a request cancelled error being returned and is not testing the intended scenario");
            }

            // The actual exception may be null if the script completed before the cancellation was observed
            if (actualException != null)
            {
                var expectedException = new ExceptionContractAssertionBuilder(FailureScenario.ScriptExecutionCancelled, tentacleConfigurationTestCase.TentacleType, clientAndTentacle).Build();
                actualException.ShouldMatchExceptionContract(expectedException);
            }

            latestException.Should().BeRequestCancelledException(rpcCallStage);

            // Complete Script was cancelled quickly
            cancellationDuration.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(30));
            
            recordedUsages.ForStartScriptAsync().Started.Should().Be(1);
            recordedUsages.ForGetStatusAsync().Started.Should().BeGreaterThanOrEqualTo(1);
            recordedUsages.ForCancelScriptAsync().Started.Should().Be(0);
            recordedUsages.ForCompleteScriptAsync().Started.Should().BeGreaterOrEqualTo(1);
        }

        async Task PauseOrStopPortForwarder(RpcCallStage rpcCallStage, PortForwarder portForwarder, IResponseMessageTcpKiller responseMessageTcpKiller, Reference<bool> rpcCallHasStarted)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                Logger.Information("Killing the port forwarder so the next RPCs are in the connecting state when being cancelled");
                portForwarder.EnterKillNewAndExistingConnectionsMode();

                await SetRpcCallWeAreInterestedInAsStarted(rpcCallHasStarted);
            }
            else
            {
                Logger.Information("Will Pause the port forwarder on next response so the next RPC is in-flight when being cancelled");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                responseMessageTcpKiller.PauseConnectionOnNextResponse(() => SetRpcCallWeAreInterestedInAsStarted(rpcCallHasStarted));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            static async Task SetRpcCallWeAreInterestedInAsStarted(Reference<bool> rpcCallHasStarted)
            {
                // Allow the port forwarder some time to stop
                await Task.Delay(TimeSpan.FromSeconds(5));
                rpcCallHasStarted.Value = true;
            }
        }

        void UnPauseOrRestartPortForwarder(TentacleType tentacleType, RpcCallStage rpcCallStage, Reference<PortForwarder> portForwarder)
        {
            if (rpcCallStage == RpcCallStage.Connecting)
            {
                Logger.Information("Starting the PortForwarder as we stopped it to get the StartScript RPC call in the Connecting state");
                portForwarder.Value.ReturnToNormalMode();
            }
            else if (tentacleType == TentacleType.Polling)
            {
                Logger.Information("UnPausing the PortForwarder as we paused the connections which means Polling will be stalled");
                portForwarder.Value.UnPauseExistingConnections();
                portForwarder.Value.CloseExistingConnections();
            }
        }

        async Task<(ScriptExecutionResult response, Exception? actualException, TimeSpan cancellationDuration)> ExecuteScriptThenCancelExecutionWhenRpcCallHasStarted(
            ClientAndTentacle clientAndTentacle,
            StartScriptCommandV3Alpha startScriptCommand,
            Reference<bool> rpcCallHasStarted,
            SemaphoreSlim whenTheRequestCanBeCancelled)
        {
            var cancelExecutionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(
                startScriptCommand,
                cancelExecutionCancellationTokenSource.Token);

            Logger.Information("Waiting for the RPC Call to start");
            await Wait.For(() => rpcCallHasStarted.Value, CancellationToken);
            Logger.Information("RPC Call has start");

            var cancellationDuration = new Stopwatch();
            await whenTheRequestCanBeCancelled.WithLockAsync(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(6), CancellationToken);
                Logger.Information("Cancelling ExecuteScript");
                cancelExecutionCancellationTokenSource.Cancel();
                cancellationDuration.Start();
            }, CancellationToken);

            Exception? actualException = null;
            (ScriptExecutionResult Response, List<ProcessOutput> Logs)? responseAndLogs = null;
            try
            {
                responseAndLogs = await executeScriptTask;
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            cancellationDuration.Stop();
            return (responseAndLogs?.Response, actualException, cancellationDuration.Elapsed);
        }
    }
}