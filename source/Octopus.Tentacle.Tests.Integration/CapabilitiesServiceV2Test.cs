using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceV2Test : IntegrationTest
    {
        [TestCase(null)] // The version of tentacle compiled from the current code.
        [TestCase("5.0.4")] // First linux Release 9/9/2019
        [TestCase("5.0.12")] // The autofac service was in octopus shared.
        [TestCase("6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(string version)
        {
            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(TentacleType.Polling)
                .WithTentacleVersion(version)
                .Build(CancellationToken);

            var capabilities = clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }
    }
}