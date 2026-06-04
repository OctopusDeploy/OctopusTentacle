#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Services.Scripts;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

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

            var capabilities = (await clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilitiesAsync(new(CancellationToken))).SupportedCapabilities;

            var expected = new List<string> { nameof(IScriptService), nameof(IFileTransferService) };
            if (version.HasScriptServiceV2())
                expected.Add(nameof(IScriptServiceV2));
            if (version.HasAbandonScript())
                expected.Add(nameof(ScriptServiceV2.AbandonScriptAsync));

            // Exact set, not just Contain: this also guards against over-advertising a capability we didn't intend.
            capabilities.Should().BeEquivalentTo(expected);
        }

        [Test]
        [TentacleConfigurations]
        public async Task CapabilitiesServiceDoesNotReturnKubernetesScriptServiceForNonKubernetesTentacle(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var version = tentacleConfigurationTestCase.Version;

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);

            var capabilities = (await clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilitiesAsync(new(CancellationToken))).SupportedCapabilities;

            var expected = new List<string> { nameof(IScriptService), nameof(IFileTransferService) };
            if (version.HasScriptServiceV2())
                expected.Add(nameof(IScriptServiceV2));
            if (version.HasAbandonScript())
                expected.Add(nameof(ScriptServiceV2.AbandonScriptAsync));

            // Exact set, not just Contain: this also guards against over-advertising. In particular a
            // non-Kubernetes Tentacle must never surface IKubernetesScriptServiceV1.
            capabilities.Should().BeEquivalentTo(expected);
            capabilities.Should().NotContain(nameof(IKubernetesScriptServiceV1));
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
                    .DecorateCapabilitiesServiceV2With(d => d
                        .DecorateGetCapabilitiesWith(
                            async (inner, options) =>
                            {
                                var response = await inner.GetCapabilitiesAsync(options);

                                capabilitiesResponses.Add(response);

                                if (resumePortForwarder)
                                {
                                    // (2) Once a get capabilities call has been made which uses the cached response then resume normal RPC calls
                                    // to allow script execution to continue
                                    portForwarder.Value.ReturnToNormalMode();
                                }

                                return response;
                            }))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(b => b
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
