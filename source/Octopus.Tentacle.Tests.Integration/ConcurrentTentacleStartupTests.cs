#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    /// <summary>
    /// Tests run in parallel and start their Tentacles as they go, which is only safe because the state those
    /// Tentacles share is created once up front - see <see cref="SharedTentacleCryptoState"/>. These tests make
    /// that assumption explicit rather than leaving it to be rediscovered as flakiness somewhere else.
    /// </summary>
    [IntegrationTestTimeout]
    public class ConcurrentTentacleStartupTests : IntegrationTest
    {
        const int ConcurrentTentacles = 4;

        [Test]
        public async Task SeveralTentaclesCanStartAtTheSameTime()
        {
            var runningTentacles = new List<RunningTentacle>();

            try
            {
                var builds = Enumerable.Range(0, ConcurrentTentacles)
                    .Select(_ => new ListeningTentacleBuilder(TestCertificates.ServerPublicThumbprint, TentacleVersions.Current).Build(Logger, CancellationToken))
                    .ToArray();

                runningTentacles.AddRange(await Task.WhenAll(builds));

                runningTentacles.Should().OnlyContain(t => t.Thumbprint == TestCertificates.TentaclePublicThumbprint,
                    "every Tentacle should have loaded the test certificate, not generated one of its own");

                runningTentacles.Select(t => t.ServiceUri.Port).Should().OnlyHaveUniqueItems();
            }
            finally
            {
                await Task.WhenAll(runningTentacles.Select(async t => await t.DisposeAsync()));
            }
        }
    }
}
