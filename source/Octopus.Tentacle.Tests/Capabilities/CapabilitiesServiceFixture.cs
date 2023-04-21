using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Services.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class CapabilitiesServiceFixture
    {
        [Test]
        public void CapabilitiesAreReturned()
        {
            new CapabilitiesService()
                .GetCapabilities()
                .SupportedCapabilities
                .Count
                .Should().Be(0);
        }
    }
}