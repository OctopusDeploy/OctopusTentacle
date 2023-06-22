using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionGathersRpcCallMetrics : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task ExecuteScriptShouldGatherMetrics(TentacleType tentacleType, string tentacleVersion)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithRpcCallObserver(rpcCallObserver)
                .Build(CancellationToken);

            var scriptBuilder = new ScriptBuilder()
                .Print("Hello");
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(scriptBuilder)
                .Build();

            // Act
            var (finalResponse, _) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);

            // Different calls are made for different tentacle versions.
            // The thing they all have in common is that there should be at least 1 metric, and it should have succeeded.
            rpcCallObserver.Metrics.Should().NotBeEmpty();
            var rpcCallMetrics = rpcCallObserver.Metrics[0];
            rpcCallMetrics.AttemptsSucceeded.Should().BeTrue();
            rpcCallMetrics.Attempts.Should().NotBeEmpty();
        }
    }
}