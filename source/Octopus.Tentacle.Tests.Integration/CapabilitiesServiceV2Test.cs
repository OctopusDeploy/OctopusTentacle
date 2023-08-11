#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceInterestingTentacles : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return AllCombinations
                .Of(TentacleType.Polling,
                    TentacleType.Listening)
                .And(
                    TentacleVersions.Current,
                    TentacleVersions.v5_0_4_FirstLinuxRelease,
                    TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only, // the autofac service is in tentacle, but tentacle does not have the capabilities service.
                    TentacleVersions.v7_0_1_ScriptServiceV2Added
                )
                .And(
                    SyncOrAsyncHalibut.Sync,
                    SyncOrAsyncHalibut.Async
                )
                .Build();
        }
    }

    [IntegrationTestTimeout]
    public class CapabilitiesServiceV2Test : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(CapabilitiesServiceInterestingTentacles))]
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(
            TentacleType tentacleType,
            Version? version, 
            SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(version)
                .Build(CancellationToken);

            var capabilities = await syncOrAsyncHalibut
                .WhenSync(() => clientAndTentacle.TentacleClient.CapabilitiesServiceV2.SyncService.GetCapabilities().SupportedCapabilities)
                .WhenAsync(async () => (await clientAndTentacle.TentacleClient.CapabilitiesServiceV2.AsyncService.GetCapabilitiesAsync(new(CancellationToken, null))).SupportedCapabilities);

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
        [TestCaseSource(typeof(CapabilitiesServiceInterestingTentacles))]
        public async Task CapabilitiesResponseShouldBeCached(TentacleType tentacleType, Version? version, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            var capabilitiesResponses = new List<CapabilitiesResponseV2>();
            var resumePortForwarder = false;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithPortForwarder(out var portForwarder)
                .WithTentacleVersion(version)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateCapabilitiesServiceV2With(d => d
                        .AfterGetCapabilities(async (response) =>
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
            catch (HalibutClientException) when (tentacleType == TentacleType.Polling && version != null)
            {
                // For script execution on a tentacle without ScriptServiceV2 and retries a polling request can be de-queued into a broken TCP Connection
                // By the time this happens we will have already called gt capabilities and got the cached response so we can safely ignore.
            }

            capabilitiesResponses.Should().HaveCount(2);
            capabilitiesResponses[0].Should().BeEquivalentTo(capabilitiesResponses[1]);
        }
    }
}
