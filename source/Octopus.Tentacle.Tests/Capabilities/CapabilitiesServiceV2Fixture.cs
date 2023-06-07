using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Services.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class CapabilitiesServiceV2Fixture
    {
        [Test]
        public void CapabilitiesAreReturned()
        {
            var capabilities = new CapabilitiesServiceV2()
                .GetCapabilities()
                .SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Should().Contain("IScriptServiceV2Alpha");
            capabilities.Count.Should().Be(3);
        }
    }
}