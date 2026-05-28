using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Services.Scripts;
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

            capabilities.Should().BeEquivalentTo(nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2), nameof(ScriptServiceV2.AbandonScriptAsync));

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

            capabilities.Should().NotContainMatch("IScriptService*");

            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
        }

        [Test]
        public async Task GetCapabilities_OnNonKubernetesTentacle_AdvertisesAbandonScriptV2()
        {
            var service = new CapabilitiesServiceV2();
            var response = await service.GetCapabilitiesAsync(CancellationToken.None);
            response.SupportedCapabilities.Should().Contain(nameof(ScriptServiceV2.AbandonScriptAsync));
        }

        [Test]
        public async Task GetCapabilities_OnKubernetesTentacle_DoesNotAdvertiseAbandonScriptV2()
        {
            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, "ABC");

            var service = new CapabilitiesServiceV2();
            var response = await service.GetCapabilitiesAsync(CancellationToken.None);
            response.SupportedCapabilities.Should().NotContain(nameof(ScriptServiceV2.AbandonScriptAsync));

            Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null);
        }
    }
}