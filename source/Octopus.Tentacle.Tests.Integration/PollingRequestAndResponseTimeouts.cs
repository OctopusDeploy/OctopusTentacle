using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class PollingRequestAndResponseTimeouts : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_ForARequestThatHasNotBeenDequeuedByTentacle_TheRequestShouldTimeout(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithRetriesDisabled()
                .WithPortForwarder()
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptServiceMethodUsages)
                    .Build())
                .Build(CancellationToken);

            // Stop the request from being dequeued by stopping the Tentacle from connecting
            clientTentacle.PortForwarder!.EnterKillNewAndExistingConnectionsMode();
            
            var startScriptCommand = new LatestStartScriptCommandBuilder().WithScriptBody(new ScriptBuilder().Print("Script Executed")).Build();

            (await AssertionExtensions.Should(() => clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken)).ThrowAsync<HalibutClientException>())
                .And.Message.Should().Contain("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (00:00:05), so the request timed out.");
            
            capabilitiesMethodUsages.ForGetCapabilitiesAsync().Completed.Should().Be(1);
            scriptServiceMethodUsages.ForStartScriptAsync().Completed.Should().Be(0, "Get Capabilities should have timed out and cancelled script execution");
        }

        [Test]
        [TentacleConfigurations(testListening: false)]
        public async Task WhenThePollingRequestQueueTimeoutIsReached_ForARequestBeingProcessedByTentacle_AndTheResponseIsReceivedBeforeThePollingRequestMaximumMessageProcessingTimeoutIsReached_TheRequestShouldSucceed(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var pollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            var buffer = TimeSpan.FromSeconds(5);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .WithRetriesDisabled()
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = pollingRequestQueueTimeout;
                    serviceEndpoint.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromHours(1);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptServiceMethodUsages)
                    .Build())
                .Build(CancellationToken);

            // Ensure the script takes longer than the pollingRequestQueueTimeout but less than the PollingRequestMaximumMessageProcessingTimeout
            var scriptDuration = pollingRequestQueueTimeout + buffer;

            var startScriptCommand = new LatestStartScriptCommandBuilder().WithScriptBody(new ScriptBuilder()
                    .Sleep(scriptDuration)
                    .Print("Script Executed"))
                    // Allow the Start Script call to take longer than the script being executed
                    .WithDurationStartScriptCanWaitForScriptToFinish(scriptDuration * 2)
                    .Build();

            var (finalResponse, _) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            capabilitiesMethodUsages.ForGetCapabilitiesAsync().Completed.Should().Be(1);
            scriptServiceMethodUsages.ForStartScriptAsync().Completed.Should().Be(1);
            scriptServiceMethodUsages.ForGetStatusAsync().Completed.Should().Be(0, "The test should have allowed StartScript long enough to complete the script, ensuring it took longer than PollingRequestQueueTimeout");
            scriptServiceMethodUsages.ForCompleteScriptAsync().Completed.Should().Be(1);
        }

        [Test]
        [TentacleConfigurations(testListening: false)]
        public async Task WhenThePollingRequestMaximumMessageProcessingTimeoutIsReached_ForARequestBeingProcessedByTentacle_TheRequestShouldTimeout(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var pollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
            var pollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(10);
            var buffer = TimeSpan.FromSeconds(5);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder()
                .WithRetriesDisabled()
                .WithServiceEndpointModifier(serviceEndpoint =>
                {
                    serviceEndpoint.PollingRequestQueueTimeout = pollingRequestQueueTimeout;
                    serviceEndpoint.PollingRequestMaximumMessageProcessingTimeout = pollingRequestMaximumMessageProcessingTimeout;
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientCapabilitiesServiceV2>(out var capabilitiesMethodUsages)
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var scriptServiceMethodUsages)
                    .Build())
                .Build(CancellationToken);

            // Ensure the script takes longer than the pollingRequestQueueTimeout + pollingRequestMaximumMessageProcessingTimeout
            var scriptDuration = pollingRequestQueueTimeout + pollingRequestMaximumMessageProcessingTimeout + buffer + buffer;

            var startScriptCommand = new LatestStartScriptCommandBuilder().WithScriptBody(new ScriptBuilder()
                    .Sleep(scriptDuration)
                    .Print("Script Executed"))
                    // Allow the Start Script call to take longer than the script being executed
                    .WithDurationStartScriptCanWaitForScriptToFinish(scriptDuration * 2)
                    .Build();

            (await AssertionExtensions.Should(() => clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken)).ThrowAsync<HalibutClientException>())
                .And.Message.Should().Contain("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time (00:00:10), so the request timed out.");
            
            capabilitiesMethodUsages.ForGetCapabilitiesAsync().Completed.Should().Be(1);
            scriptServiceMethodUsages.ForStartScriptAsync().Completed.Should().Be(1);
            scriptServiceMethodUsages.ForGetStatusAsync().Completed.Should().Be(0, "The test should have allowed StartScript long enough to cause the PollingRequestMaximumMessageProcessingTimeout to elapse");
            scriptServiceMethodUsages.ForCancelScriptAsync().Completed.Should().Be(0);
            scriptServiceMethodUsages.ForCompleteScriptAsync().Completed.Should().Be(0);
        }
    }
}
