using System;
using System.Collections;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceInterestingTentacles : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            var versions = new[]
            {
                null, // The version of tentacle compiled from the current code.
                "5.0.4", // First linux Release 9/9/2019
                "5.0.12", // The autofac service was in octopus shared.
                "6.3.451" // the autofac service is in tentacle, but tentacle does not have the capabilities service.
            };

            return CartesianProduct.Of(new TentacleTypesToTest(), versions).GetEnumerator();
        }
    }

    [RunTestsInParallelLocallyIfEnabledButNeverOnTeamCity]
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