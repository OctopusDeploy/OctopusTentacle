using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionRpcRetryTimeoutsWork : IntegrationTest
    {

        //Retry Timeout
        // - Times out start script / get status / cancel script / complete script when connecting
        // - Times out start script / get status / cancel script / complete script for an in-flight rpc call(walks away as we can't cancel in Halibut at this point currently)
        //
        //Cancellation
        // - Can cancel a call to get capabilities without waiting (could be connecting or in-flight) and walk away
        // - Can cancel a connecting call to start script
        //    - If it is the first start script call it can walk away
        //    - If it's a retry of start script it probably should try and call cancel as we don't know if the script is running on Tentacle
        // - Can cancel an in-flight call to start script and it go into the cancel rpc call flow
        // - Can cancel a call to get status(either connecting or in-flight) without waiting and go into the cancel rpc call flow
        // - Can not cancel a call to Cancel Script
        // - Can not cancel a call to CompleteScript


        [Test]
        [TestCase(TentacleType.Polling)]
        [TestCase(TentacleType.Listening)]
        public async Task WhenRpcRetriesTimeOut_DuringStartScript_AndClientIsConnectingToTentacle_TheRpcCallIsCancelled(TentacleType tentacleType)
        {
            // WIP

            // Start Polling / Listening Tentacle
            // Start Script
            // Fail StartScript so we start retries
            // Timeout retries
            // StartScript should stop
            // Ensure the connect was cancelled

            var startScriptCallCount = 0;
            var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                .DecorateScriptServiceV2With(
                    builder => builder.DecorateStartScriptWith((inner, command) =>
                    {
                        startScriptCallCount++;
                        return inner.StartScript(command);
                    }))
                .Build();

            var builder = new ServerTentacleClientAndTentacleBuilder(tentacleType);

            var (server, portForwarder, runningTentacle, tentacleClient) = await builder
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(tentacleServicesDecorator)
                .Build(CancellationToken);

            using (server)
            using (portForwarder)
            using (runningTentacle)
            using (var temporaryDirectory = new TemporaryDirectory())
            {
                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder()
                        .Print("hello"))
                    // Configure the start script command to wait a long time, so we have plenty of time to kill the connection.
                    .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromHours(1))
                    .Build();

                var execScriptTask = tentacleClient.ExecuteScript(startScriptCommand,
                    onScriptStatusResponseReceived: response =>
                    {
                        Logger.Information($"[Script Response] " + string.Join(Environment.NewLine, response.Logs.Select(x => x.Text)));

                        //logs.AddRange(onScriptStatusResponseReceived.Logs);
                    },
                    onScriptCompleted: ct =>
                    {
                        return Task.CompletedTask;
                    },
                    Logger.ForContext<TentacleClient>().ToILog(),
                    CancellationToken);

                var finalResponse = await execScriptTask;

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                startScriptCallCount.Should().Be(1);
            }
        }
    }
}