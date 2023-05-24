using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceV2Test : IntegrationTest
    {
        [TestCase(TentacleType.Polling, null)] // The version of tentacle compiled from the current code.
        [TestCase(TentacleType.Polling, "5.0.4")] // First linux Release 9/9/2019
        [TestCase(TentacleType.Polling, "5.0.12")] // The autofac service was in octopus shared.
        [TestCase(TentacleType.Polling, "6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        [TestCase(TentacleType.Listening, null)]
        [TestCase(TentacleType.Listening, "5.0.4")]
        [TestCase(TentacleType.Listening, "5.0.12")]
        [TestCase(TentacleType.Listening, "6.3.451")]
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(TentacleType tentacleType, string version)
        {
            using var clientAndTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .Build(CancellationToken);

            var capabilities = clientAndTentacle.TentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }
    }
}