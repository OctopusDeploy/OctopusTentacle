using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// These tests make sure that we can cancel or walk away (if code does not cooperate with cancellation tokens)
    /// from RPC calls when they are being retried and the rpc timeout period elapses.
    /// </summary>
    public class ClientScriptExecutionRpcRetryTimeoutsWork : IntegrationTest
    {
        [Test]
        [TestCase(TentacleType.Polling, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Polling, true)] // Timeout trying to connect
        [TestCase(TentacleType.Listening, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Listening, true)] // Timeout trying to connect
        public async Task WhenRpcRetriesTimeOut_DuringStartScript_TheRpcCallIsCancelled(TentacleType tentacleType, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(10))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exception)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeStartScript(() =>
                        {
                            // Kill the first StartScript call to force the rpc call into retries
                            if (scriptServiceV2Exception.StartScriptLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (stopPortForwarderAfterFirstCall)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder!.Dispose();
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

            portForwarder = clientAndTentacle.PortForwarder;

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                .Build();

            // Start the script which will wait for a file to exist
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
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(9));
        }

        [Test]
        [TestCase(TentacleType.Polling, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Polling, true)] // Timeout trying to connect
        [TestCase(TentacleType.Listening, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Listening, true)] // Timeout trying to connect
        public async Task WhenRpcRetriesTimeOut_DuringGetStatus_TheRpcCallIsCancelled(TentacleType tentacleType, bool stopPortForwarderAfterFirstCall)
        {
            PortForwarder portForwarder = null!;
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(10))
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToScriptServiceV2()
                    .CountCallsToScriptServiceV2(out var scriptServiceCallCounts)
                    .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2Exception)
                    .DecorateScriptServiceV2With(d => d
                        .BeforeGetStatus(() =>
                        {
                            // Kill the first GetStatus call to force the rpc call into retries
                            if (scriptServiceV2Exception.GetStatusLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (stopPortForwarderAfterFirstCall)
                                {
                                    // Kill the port forwarder so the next requests are in the connecting state when retries timeout
                                    Logger.Information("Killing PortForwarder");
                                    portForwarder!.Dispose();
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

            portForwarder = clientAndTentacle.PortForwarder;

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Start Script")
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                // Don't wait in start script as we want to tst get status
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Wait for StartScript to start and then kill the connection
            var lastObservedGetStatusCallCount = 0L;
            await Wait.For(() =>
            {
                lastObservedGetStatusCallCount = scriptServiceCallCounts.GetStatusCallCountStarted;
                return lastObservedGetStatusCallCount > 0;
            }, CancellationToken);

            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            scriptServiceCallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(2);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            // Ensure we actually waited and retried until the timeout policy kicked in
            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(9));
        }


        //Retry Timeout
        // - Times out cancel script / complete script when connecting
        // - Times out cancel script / complete script for an in-flight rpc call(walks away as we can't cancel in Halibut at this point currently)
        // - Timeout for file upload and file download work
        //Cancellation
        // - Can cancel a call to get capabilities without waiting (could be connecting or in-flight) and walk away
        // - Can cancel a connecting call to start script
        //    - If it is the first start script call it can walk away
        //    - If it's a retry of start script it probably should try and call cancel as we don't know if the script is running on Tentacle
        // - Can cancel an in-flight call to start script and it go into the cancel rpc call flow
        // - Can cancel a call to get status(either connecting or in-flight) without waiting and go into the cancel rpc call flow
        // - Can not cancel a call to Cancel Script
        // - Can not cancel a call to CompleteScript
    }
}