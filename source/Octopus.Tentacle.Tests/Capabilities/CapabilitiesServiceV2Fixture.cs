using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Services.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class CapabilitiesServiceV2Fixture
    {
        [Test]
        public async Task CapabilitiesAreReturned()
        {
            var capabilities = (await new CapabilitiesServiceV2()
                .GetCapabilitiesAsync(CancellationToken.None))
                .SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Should().Contain("IScriptServiceV2");
            capabilities.Should().Contain("IScriptServiceV3Alpha");
            capabilities.Count.Should().Be(4);
        }
    }
}