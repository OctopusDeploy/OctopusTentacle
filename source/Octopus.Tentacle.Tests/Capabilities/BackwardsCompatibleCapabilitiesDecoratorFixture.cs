using FluentAssertions;
using Halibut.Exceptions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class BackwardsCompatibleCapabilitiesDecoratorFixture
    {
        [Test]
        public void ShouldWrapServiceNotFoundExceptionToNoCapabilities()
        {
            var backwardsCompatibleCapabilitiesService = new ThrowsServiceNotFoundCapabilitiesService().WithBackwardsCompatability();
            backwardsCompatibleCapabilitiesService.GetCapabilities().capabilities.Count.Should().Be(0);
        }

        public class ThrowsServiceNotFoundCapabilitiesService : ICapabilitiesService
        {
            public CapabilitiesResponse GetCapabilities()
            {
                throw new ServiceNotFoundHalibutClientException("Nope", "Can't find it");
            }
        }
    }
}