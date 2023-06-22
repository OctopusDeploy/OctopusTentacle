using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientGathersRpcCallMetrics : IntegrationTest
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
            rpcCallObserver.Metrics.Should().ContainSingle(m => m.RpcCallName == "StartScript");
            var metric = rpcCallObserver.Metrics[0];
            metric.AttemptsSucceeded.Should().BeTrue();
            metric.Attempts.Should().NotBeEmpty();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task UploadFileShouldGatherMetrics(TentacleType tentacleType, string version)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithRpcCallObserver(rpcCallObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Assert
            rpcCallObserver.Metrics.Should().HaveCountGreaterThan(0);
            var metric = rpcCallObserver.Metrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.UploadFile));
            metric.AttemptsSucceeded.Should().BeTrue();
            metric.Attempts.Should().NotBeEmpty();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task DownloadFileShouldGatherMetrics(TentacleType tentacleType, string version)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithRpcCallObserver(rpcCallObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "DownloadFile.txt");
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Act
            await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);

            // Assert
            rpcCallObserver.Metrics.Should().HaveCountGreaterThan(1); // the first one will be the upload
            var metric = rpcCallObserver.Metrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.DownloadFile));
            metric.AttemptsSucceeded.Should().BeTrue();
            metric.Attempts.Should().NotBeEmpty();
        }
    }
}