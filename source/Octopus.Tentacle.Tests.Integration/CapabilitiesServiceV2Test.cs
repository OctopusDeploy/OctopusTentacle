#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class CapabilitiesServiceV2Test : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCapabilitiesServiceVersions: true)]
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            Version? version = tentacleConfigurationTestCase.Version;

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);

            var capabilities = (await clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilitiesAsync(new(CancellationToken, null))).SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");

            if (version.HasScriptServiceV2())
            {
                capabilities.Should().Contain("IScriptServiceV2");
                capabilities.Count.Should().Be(3);
            }
            else
            {
                capabilities.Count.Should().Be(2);
            }
        }

        [Test]
        [TentacleConfigurations(testCapabilitiesServiceVersions: true)]
        public async Task CapabilitiesResponseShouldBeCached(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var capabilitiesResponses = new List<CapabilitiesResponseV2>();
            var resumePortForwarder = false;

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarder(out var portForwarder)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .HookServiceMethod<IAsyncClientCapabilitiesServiceV2, object, CapabilitiesResponseV2>(nameof(IAsyncClientCapabilitiesServiceV2.GetCapabilitiesAsync),
                        null,
                        async (_, response) =>
                        {
                            await Task.CompletedTask;

                            capabilitiesResponses.Add(response);

                            if (resumePortForwarder)
                            {
                                // (2) Once a get capabilities call has been made which uses the cached response then resume normal RPC calls
                                // to allow script execution to continue
                                portForwarder.Value.ReturnToNormalMode();
                            }
                        })
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b
                    .Print("Running..."))
                .Build();

            await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // (1) Kill new and existing connections to ensure no RPC calls can be made
            clientAndTentacle.PortForwarder!.EnterKillNewAndExistingConnectionsMode();
            resumePortForwarder = true;

            try
            {
                await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
            }
            catch (HalibutClientException) when (tentacleConfigurationTestCase.TentacleType == TentacleType.Polling && tentacleConfigurationTestCase.Version != null)
            {
                // For script execution on a tentacle without ScriptServiceV2 and retries a polling request can be de-queued into a broken TCP Connection
                // By the time this happens we will have already called gt capabilities and got the cached response so we can safely ignore.
            }

            capabilitiesResponses.Should().HaveCount(2);
            capabilitiesResponses[0].Should().BeEquivalentTo(capabilitiesResponses[1]);
        }
    }
}
