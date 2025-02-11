using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
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
            var k8sDetection = Substitute.For<IKubernetesAgentDetection>();
            k8sDetection.IsRunningAsKubernetesAgent.Returns(false);
            
            var capabilities = (await new CapabilitiesServiceV2(k8sDetection)
                .GetCapabilitiesAsync(CancellationToken.None))
                .SupportedCapabilities;

            capabilities.Should().BeEquivalentTo(nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2));
            capabilities.Count.Should().Be(3);

            capabilities.Should().NotContainMatch("IKubernetesScriptService*");
        }

        [Test]
        public async Task OnlyKubernetesScriptServicesAreReturnedWhenRunningAsKubernetesAgent()
        {
            var k8sDetection = Substitute.For<IKubernetesAgentDetection>();
            k8sDetection.IsRunningAsKubernetesAgent.Returns(true);

            var capabilities = (await new CapabilitiesServiceV2(k8sDetection)
                    .GetCapabilitiesAsync(CancellationToken.None))
                .SupportedCapabilities;

            capabilities.Should().BeEquivalentTo(nameof(IFileTransferService), nameof(IKubernetesScriptServiceV1));
            capabilities.Count.Should().Be(2);

            capabilities.Should().NotContainMatch("IScriptService*");
        }
    }
}