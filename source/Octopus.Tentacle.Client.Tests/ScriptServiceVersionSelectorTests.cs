using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ScriptServiceVersionSelectorTests
    {
        [Test]
        public async Task WhenTheTentacleAdvertisesAbandon_SupportsAbandonIsTrue()
        {
            var (version, supportsAbandon) = await SelectFor(
                nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2), nameof(IAsyncClientScriptServiceV2.AbandonScriptAsync));

            version.Should().Be(ScriptServiceVersion.ScriptServiceVersion2);
            supportsAbandon.Should().BeTrue();
        }

        [Test]
        public async Task WhenAnOlderV2TentacleDoesNotAdvertiseAbandon_SupportsAbandonIsFalse()
        {
            // Old V2 Tentacles are V2 but predate the abandon verb. Gating on "is V2" would wrongly let the
            // orchestrator call AbandonScript on them; gating on the advertised capability does not.
            var (version, supportsAbandon) = await SelectFor(
                nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2));

            version.Should().Be(ScriptServiceVersion.ScriptServiceVersion2);
            supportsAbandon.Should().BeFalse();
        }

        [Test]
        public async Task WhenTheTentacleOnlyHasV1_SupportsAbandonIsFalse()
        {
            var (version, supportsAbandon) = await SelectFor(
                nameof(IScriptService), nameof(IFileTransferService));

            version.Should().Be(ScriptServiceVersion.ScriptServiceVersion1);
            supportsAbandon.Should().BeFalse();
        }

        static async Task<(ScriptServiceVersion Version, bool SupportsAbandon)> SelectFor(params string[] capabilities)
        {
            var capabilitiesService = Substitute.For<IAsyncClientCapabilitiesServiceV2>();
            capabilitiesService.GetCapabilitiesAsync(Arg.Any<HalibutProxyRequestOptions>())
                .Returns(Task.FromResult(new CapabilitiesResponseV2(new List<string>(capabilities))));

            var selector = new ScriptServiceVersionSelector(
                capabilitiesService,
                Substitute.For<ITentacleClientTaskLog>(),
                RpcCallExecutorFactory.Create(TimeSpan.Zero, Substitute.For<ITentacleClientObserver>()),
                new TentacleClientOptions(new RpcRetrySettings(RetriesEnabled: false, RetryDuration: TimeSpan.Zero)),
                ClientOperationMetricsBuilder.Start());

            return await selector.DetermineScriptServiceVersionToUse(CancellationToken.None);
        }
    }
}
