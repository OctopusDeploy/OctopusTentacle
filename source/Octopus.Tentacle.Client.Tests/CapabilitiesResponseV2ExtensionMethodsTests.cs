using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class CapabilitiesResponseV2ExtensionMethodsTests
    {
        [Test]
        public void HasAbandonScriptV2_WhenAdvertised_ReturnsTrue()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2", "AbandonScriptAsync" });
            capabilities.HasAbandonScriptV2().Should().BeTrue();
        }

        [Test]
        public void HasAbandonScriptV2_WhenNotAdvertised_ReturnsFalse()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string> { "IScriptServiceV2" });
            capabilities.HasAbandonScriptV2().Should().BeFalse();
        }

        [Test]
        public void HasAbandonScriptV2_WhenEmpty_ReturnsFalse()
        {
            var capabilities = new CapabilitiesResponseV2(new List<string>());
            capabilities.HasAbandonScriptV2().Should().BeFalse();
        }
    }
}
