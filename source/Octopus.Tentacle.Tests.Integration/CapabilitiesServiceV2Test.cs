using System;
using System.Collections;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

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
                capabilities.Should().Contain("IScriptServiceV2Alpha");
                capabilities.Count.Should().Be(3);
            }
            else
            {
                capabilities.Count.Should().Be(2);
            }
        }
    }
}