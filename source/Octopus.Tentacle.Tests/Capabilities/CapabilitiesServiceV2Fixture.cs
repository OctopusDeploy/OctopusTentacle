using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Kubernetes;
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

            capabilities.Should().BeEquivalentTo(nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2));
            capabilities.Count.Should().Be(3);

            capabilities.Should().NotContainMatch("IKubernetesScriptService*");
        }

        [Test]
        public async Task OnlyKubernetesScriptServicesAreReturnedWhenRunningAsKubernetesAgent()
        {
            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, "ABC");

            var capabilities = (await new CapabilitiesServiceV2()
                    .GetCapabilitiesAsync(CancellationToken.None))
                .SupportedCapabilities;

            capabilities.Should().BeEquivalentTo(nameof(IFileTransferService), nameof(IKubernetesScriptServiceV1));
            capabilities.Count.Should().Be(3);

            capabilities.Should().NotContainMatch("IScriptService*");

            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
        }
    }
}