using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Services.Capabilities;

namespace Octopus.Tentacle.Tests.Capabilities
{
    public class CapabilitiesServiceV2Fixture
    {
        [Test]
        public async Task NonKubernetesScriptServicesAreReturnedForNonKubernetesAgent()
        {
            var capabilities = (await new CapabilitiesServiceV2()
                .GetCapabilitiesAsync(CancellationToken.None))
                .SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            capabilities.Should().Contain("IScriptServiceV2");
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

            capabilities.Should().Contain("IFileTransferService");
            capabilities.Should().Contain("IKubernetesScriptServiceV1Alpha");
            capabilities.Count.Should().Be(2);


            capabilities.Should().NotContainMatch("IScriptService*");
        }
    }
}