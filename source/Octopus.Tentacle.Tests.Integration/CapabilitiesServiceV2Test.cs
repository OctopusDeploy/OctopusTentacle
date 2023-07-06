using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceInterestingTentacles : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return AllCombinations
                .Of(new TentacleTypesToTest())
                .And(
                    TentacleVersions.Current,
                    TentacleVersions.v5_0_4_FirstLinuxRelease,
                    TentacleVersions.v5_0_12_AutofacServiceFactoryIsInShared,
                    TentacleVersions.v6_3_417_LastWithScriptServiceV1Only // the autofac service is in tentacle, but tentacle does not have the capabilities service.
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
            string? version)
        {
            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version!)
                .Build(CancellationToken);

            var capabilities = clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            if (version == null)
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
        public async Task CapabilitiesResponseShouldBeCached(TentacleType tentacleType, string? version)
        {
            var capabilitiesResponses = new List<CapabilitiesResponseV2>();
            var resumePortForwarder = false;

            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithPortForwarder(out var portForwarder)
                .WithTentacleVersion(version)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateCapabilitiesServiceV2With(d => d
                        .AfterGetCapabilities((response) =>
                        {
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
            catch (HalibutClientException) when (tentacleType == TentacleType.Polling && !string.IsNullOrWhiteSpace(version))
            {
                // For script execution on a tentacle without ScriptServiceV2 and retries a polling request can be de-queued into a broken TCP Connection
                // By the time this happens we will have already called gt capabilities and got the cached response so we can safely ignore.
            }

            capabilitiesResponses.Should().HaveCount(2);
            capabilitiesResponses[0].Should().BeEquivalentTo(capabilitiesResponses[1]);
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task WhenNetworkFailureOccurs_DuringGetCapabilities_AndRetriesAreDisabled_TheCallIsNotRetried(TentacleType tentacleType)
        {
            IClientScriptServiceV2? scriptServiceV2 = null;
            
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithRetriesDisabled()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithRetryDuration(TimeSpan.FromMinutes(4))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToCapabilitiesServiceV2()
                    .CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceCallCounts)
                    .RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceExceptions)
                    .DecorateCapabilitiesServiceV2With(new CapabilitiesServiceV2DecoratorBuilder()
                        .BeforeGetCapabilities(() =>
                        {
                            // Due to the GetCapabilities response getting cached, we must
                            // use a different service to ensure Tentacle is connected to Server.
                            // Otherwise, the response to the 'ensure connection' will get cached
                            // and any subsequent calls will succeed w/o using the network.
                            scriptServiceV2!.EnsureTentacleIsConnectedToServer(Logger);
                            
                            if (capabilitiesServiceExceptions.GetCapabilitiesLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        })
                        .Build())
                    .Build())
                .Build(CancellationToken);
            
            scriptServiceV2 = clientTentacle.Server.ServerHalibutRuntime.CreateClient<IScriptServiceV2, IClientScriptServiceV2>(clientTentacle.ServiceEndPoint);
            
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().Print("hello")).Build();

            var logs = new List<ProcessOutput>();
            Assert.ThrowsAsync<HalibutClientException>(async () => await clientTentacle.TentacleClient.ExecuteScriptAssumingException(startScriptCommand, logs, CancellationToken));

            var allLogs = logs.JoinLogs();

            allLogs.Should().NotContain("hello");
            capabilitiesServiceExceptions.GetCapabilitiesLatestException.Should().NotBeNull();
            capabilitiesServiceCallCounts.GetCapabilitiesCallCountStarted.Should().Be(1);
            capabilitiesServiceCallCounts.GetCapabilitiesCallCountComplete.Should().Be(1);
        }
    }
}
