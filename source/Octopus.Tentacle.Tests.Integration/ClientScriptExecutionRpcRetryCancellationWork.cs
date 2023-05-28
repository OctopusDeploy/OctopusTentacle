using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading;
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
    /// These tests make sure that we can cancel the ExecuteScript operation when using Tentacle Client.
    /// </summary>
    [IntegrationTestTimeout]
    public class ClientScriptExecutionCanBeCancelled : IntegrationTest
    {
        // Get capabilities
        // - First call - Connecting - Cancel immediately - Do not go in CancelScript flow
        // - First call - In-Flight - Cancel immediately - Do not go in CancelScript flow
        // - Retries - Connecting - Cancel immediately - Do not go in CancelScript flow
        // - Retries - In-Flight - Cancel immediately - Do not go in CancelScript flow
        [Test]
        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, false)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, false)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, false)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, false)]

        [TestCase(TentacleType.Polling, RpcCallStage.InFlight, true)]
        [TestCase(TentacleType.Polling, RpcCallStage.Connecting, true)]
        [TestCase(TentacleType.Listening, RpcCallStage.InFlight, true)]
        [TestCase(TentacleType.Listening, RpcCallStage.Connecting, true)]
        public async Task DuringGetCapabilities_TheRpcCallIsCancelledImmediately(TentacleType tentacleType, RpcCallStage rpcCallStage, bool duringRetries)
        {
            var rpcCallHasStarted = false;

            PortForwarder portForwarder = null!;
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetryDuration(duringRetries ? TimeSpan.FromHours(1) : TimeSpan.Zero)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogAndCountAllCalls(out var capabilitiesServiceV2CallCounts, out _, out var scriptServiceV2CallCounts, out _)
                    .RecordAllExceptions(out var capabilityServiceV2Exceptions, out _, out _, out _)
                    .DecorateCapabilitiesServiceV2With(d => d
                        .BeforeGetCapabilities((service) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);

                            // If {duringRetries} then Kill the first GetCapabilities call to force the rpc call into retries
                            if (duringRetries && capabilityServiceV2Exceptions.GetCapabilitiesLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                            else
                            {
                                if (rpcCallStage == RpcCallStage.Connecting)
                                {
                                    Logger.Information("Killing the port forwarder so the next RPCs are in the connecting state when being cancelled");
                                    portForwarder!.Dispose();
                                    rpcCallHasStarted = true;
                                }
                                else
                                {
                                    Logger.Information("Will Pause the port forwarder on next response so the next RPC is in-flight when being cancelled");
                                    responseMessageTcpKiller.PauseConnectionOnNextResponse(() => rpcCallHasStarted = true);
                                }
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);
            portForwarder = clientAndTentacle.PortForwarder;

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Should not run this script"))
                .Build();

            var cancelExecutionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, cancelExecutionCancellationTokenSource.Token);
            Func<Task> action = async () => await executeScriptTask;

            Logger.Information("Waiting for the RPC Call to start");
            await Wait.For(() => rpcCallHasStarted, CancellationToken);

            Logger.Information("Cancelling ExecuteScript");
            var cancellationDuration = Stopwatch.StartNew();
            cancelExecutionCancellationTokenSource.Cancel();

            Exception? actualException = null;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            cancellationDuration.Stop();

            capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountStarted.Should().Be(duringRetries ? 2 : 1);
            actualException.Should().NotBeNull().And.Match(IsCancellationException());
            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(0, "Test should not have not proceeded past GetCapabilities");
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0, "Should not have tried to call CancelScript");

            // Ensure we cancelled quickly. Connecting calls should cancel almost immediately. In-Flight calls will have a 5 second delay as the RetryHandler attempts to wait
            // for the action to cancel itself before walking away after 5 seconds
            cancellationDuration.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(rpcCallStage == RpcCallStage.Connecting ? 2 : 7));

            if (rpcCallStage == RpcCallStage.Connecting)
            {
                // Ensure that connecting calls get the actual call chain cancelled and return from the
                capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountComplete.Should().Be(duringRetries ? 2 : 1);
                capabilityServiceV2Exceptions.GetCapabilitiesLatestException.Should().NotBeNull().And.Match(IsCancellationException());
            }
            else
            {
                // This isn't the desired behaviour, walking away leaving the RPC call in flight, but currently our only option. It would be nice to know when this changes
                capabilitiesServiceV2CallCounts.GetCapabilitiesCallCountComplete.Should().Be(duringRetries ? 1 : 0);
            }
        }

        private static Expression<Func<Exception, bool>> IsCancellationException()
        {
            return e => e is OperationCanceledException || (e is HalibutClientException && e.Message.Contains("The operation was canceled"));
        }

        // Start Script
        // - First call - Connecting - Cancel immediately - Do not go in CancelScript flow
        // - First call - In-Flight - Cancel immediately - Go into CancelScript flow
        // - Retries - Connecting - Cancel immediately - Go into CancelScript flow
        // - Retries - In-Flight - Cancel immediately - Go into CancelScript flow

        // Get Status
        // - First call - Connecting - Cancel immediately - Go in CancelScript flow
        // - First call - In-Flight - Cancel immediately - Go into CancelScript flow
        // - Retries - Connecting - Cancel immediately - Go into CancelScript flow
        // - Retries - In-Flight - Cancel immediately - Go into CancelScript flow

        // Cancel Script
        // Cannot be cancelled
        // Ensure the first call eventually times out (Server - Tentacle co-operative cancellation)

        // Complete Script
        // - First call - Connecting - Cancel after 1 minute (Server - Tentacle co-operative cancellation)
        // - First call - In-Flight - Cancel after 1 minute (Server - Tentacle co-operative cancellation)
        // - Tentacle should eventually clean up WorkSpaces
    }
}