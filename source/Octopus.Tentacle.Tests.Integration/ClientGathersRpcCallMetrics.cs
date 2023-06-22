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
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientGathersRpcCallMetrics : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task ExecuteScriptShouldGatherMetrics_WhenSucceeds(TentacleType tentacleType, string tentacleVersion)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithRpcCallObserver(rpcCallObserver)
                .Build(CancellationToken);
            
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            var (finalResponse, _) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);

            // Different calls are made for different tentacle versions.
            // The thing they all have in common is that there should be at least 1 metric, and it should have succeeded.
            rpcCallObserver.Metrics.Should().NotBeEmpty();
            //rpcCallObserver.Metrics.Should().ContainSingle(m => m.RpcCallName == "StartScript");
            var metric = rpcCallObserver.Metrics[0];
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task ExecuteScriptShouldGatherMetrics_WhenFails(TentacleType tentacleType, string tentacleVersion)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithRpcCallObserver(rpcCallObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(b => b.BeforeStartScript(() => throw new HalibutClientException("Error"))).Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            rpcCallObserver.Metrics.Should().NotBeEmpty();
            var startScriptMetric = rpcCallObserver.Metrics.Should().ContainSingle(m => m.RpcCallName == "StartScript").Subject;
            startScriptMetric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task UploadFileShouldGatherMetrics_WhenSucceeds(TentacleType tentacleType, string version)
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
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task UploadFileShouldGatherMetrics_WhenFails(TentacleType tentacleType, string version)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithRpcCallObserver(rpcCallObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateFileTransferServiceWith(b => b.BeforeUploadFile(() => throw new HalibutClientException("Error"))).Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            rpcCallObserver.Metrics.Should().NotBeEmpty();
            var metric = rpcCallObserver.Metrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.UploadFile));
            metric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task DownloadFileShouldGatherMetrics_WhenSucceeds(TentacleType tentacleType, string version)
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
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task DownloadFileShouldGatherMetrics_WhenFails(TentacleType tentacleType, string version)
        {
            // Arrange
            var rpcCallObserver = new TestRpcCallObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithRpcCallObserver(rpcCallObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateFileTransferServiceWith(b => b.BeforeDownloadFile(() => throw new HalibutClientException("Error"))).Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "DownloadFile.txt");
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            rpcCallObserver.Metrics.Should().HaveCountGreaterThan(1); // the first one will be the upload
            var metric = rpcCallObserver.Metrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.DownloadFile));
            metric.Succeeded.Should().BeFalse();
        }
    }
}