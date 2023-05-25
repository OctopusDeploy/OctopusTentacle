using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutionRpcRetryTimeoutsWork : IntegrationTest
    {
        [Test]
        [TestCase(TentacleType.Polling, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Polling, true)] // Timeout trying to connect
        [TestCase(TentacleType.Listening, false)] // Timeout an in-flight request
        [TestCase(TentacleType.Listening, true)] // Timeout trying to connect
        public async Task WhenRpcRetriesTimeOut_DuringStartScript_TheRpcCallIsCancelled(TentacleType tentacleType, bool stopPortForwarderAfterFirstCall)
        {
            CountingCallsScriptServiceV2Decorator scriptServiceCallCounts = null!;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                // Set a short retry duration so we cancel fairly quickly
                .WithRetryDuration(TimeSpan.FromSeconds(45))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(inner => scriptServiceCallCounts = new(inner))
                    .Build())
                .Build(CancellationToken);

            var startScriptHasStartedFile = Path.Combine(clientAndTentacle.TemporaryDirectory.DirectoryPath, "startscript.started");
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("Start Script")
                    .CreateFile(startScriptHasStartedFile)
                    .Sleep(TimeSpan.FromHours(1))
                    .Print("End Script"))
                // Make StartScript wait for a very long time so we can force a failure at this point
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromHours(1))
                .Build();

            // Start the script which will wait for a file to exist
            var duration = Stopwatch.StartNew();
            var executeScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand,
                onScriptStatusResponseReceived: _ => {},
                onScriptCompleted: _ => Task.CompletedTask,
                Logger.ForContext<TentacleClient>().ToILog(),
                CancellationToken);

            // Wait for StartScript to start and then kill the connection
            await Wait.For(() => File.Exists(startScriptHasStartedFile), CancellationToken);

            if (stopPortForwarderAfterFirstCall)
            {
                clientAndTentacle.PortForwarder.Dispose();
            }
            else
            {
                clientAndTentacle.PortForwarder.CloseExistingConnections();
            }

            // Wait for StartScript to be called again
            await Wait.For(() => scriptServiceCallCounts.StartScriptCallCountStarted > 1, CancellationToken);

            Func<Task> action = async () => await executeScriptTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            duration.Stop();

            scriptServiceCallCounts.StartScriptCallCountStarted.Should().BeGreaterOrEqualTo(2);

            scriptServiceCallCounts.GetStatusCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CancelScriptCallCountStarted.Should().Be(0);
            scriptServiceCallCounts.CompleteScriptCallCountStarted.Should().Be(0);

            duration.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(45));
        }



        //Retry Timeout
        // - Times out get status / cancel script / complete script when connecting
        // - Times out get status / cancel script / complete script for an in-flight rpc call(walks away as we can't cancel in Halibut at this point currently)
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
    }
}