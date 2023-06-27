using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class BackwardsCompatibleCapabilitiesV2DecoratorFixture
    {
        [Test]
        public void ShouldWrapHalibutServiceNotFoundExceptionToNoCapabilities()
        {
            var backwardsCompatibleCapabilitiesService = new BackwardsCompatibleCapabilitiesV2TestServices.
                ThrowsServiceNotFoundCapabilitiesService().WithBackwardsCompatability();

            var capabilities = backwardsCompatibleCapabilitiesService.GetCapabilities().SupportedCapabilities;
            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }

        [Test]
        public void ShouldWrapTentacleSpecificServiceNotFoundExceptionToNoCapabilities()
        {
            var backwardsCompatibleCapabilitiesService = new BackwardsCompatibleCapabilitiesV2TestServices
                .ThrowsTentacleSpecificServiceNotFoundCapabilitiesService().WithBackwardsCompatability();

            var capabilities = backwardsCompatibleCapabilitiesService.GetCapabilities().SupportedCapabilities;
            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Count.Should().Be(2);
        }
    }
}