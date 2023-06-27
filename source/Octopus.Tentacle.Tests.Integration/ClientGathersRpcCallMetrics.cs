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
using Octopus.Tentacle.Contracts.Observability;
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
            var tentacleClientObserver = new TestTentacleClientObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);
            
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            var (finalResponse, _) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);

            var executeScriptMetrics = tentacleClientObserver.ExecuteScriptMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeSuccessful(executeScriptMetrics);

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCallName == nameof(IClientCapabilitiesServiceV2.GetCapabilities));
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCallName == nameof(IClientScriptServiceV2.StartScript));
            tentacleClientObserver.RpcCallMetrics.Should().Contain(m => m.RpcCallName == nameof(IClientScriptServiceV2.GetStatus));
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCallName == nameof(IClientScriptServiceV2.CompleteScript));
            tentacleClientObserver.RpcCallMetrics.Should().AllSatisfy(m => m.Succeeded.Should().BeTrue());
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task ExecuteScriptShouldGatherMetrics_WhenFails(TentacleType tentacleType, string tentacleVersion)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceWith(new ScriptServiceDecoratorBuilder().BeforeStartScript(() => throw exception).Build())
                    .DecorateScriptServiceV2With(b => b.BeforeStartScript(() => throw exception))
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            var executeScriptMetrics = tentacleClientObserver.ExecuteScriptMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeFailed(executeScriptMetrics, exception);

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            var startScriptMetric = tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCallName == "StartScript").Subject;
            startScriptMetric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task UploadFileShouldGatherMetrics_WhenSucceeds(TentacleType tentacleType, string version)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Assert
            var uploadFileMetrics = tentacleClientObserver.UploadFileMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeSuccessful(uploadFileMetrics);

            tentacleClientObserver.RpcCallMetrics.Should().HaveCountGreaterThan(0);
            var metric = tentacleClientObserver.RpcCallMetrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.UploadFile));
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task UploadFileShouldGatherMetrics_WhenFails(TentacleType tentacleType, string version)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateFileTransferServiceWith(b => b.BeforeUploadFile(() => throw exception)).Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            var uploadFileMetrics = tentacleClientObserver.UploadFileMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeFailed(uploadFileMetrics, exception);

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            var metric = tentacleClientObserver.RpcCallMetrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.UploadFile));
            metric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task DownloadFileShouldGatherMetrics_WhenSucceeds(TentacleType tentacleType, string version)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "DownloadFile.txt");
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Act
            await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);

            // Assert
            var downloadFileMetrics = tentacleClientObserver.DownloadFileMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeSuccessful(downloadFileMetrics);

            tentacleClientObserver.RpcCallMetrics.Should().HaveCountGreaterThan(1); // the first one will be the upload
            var metric = tentacleClientObserver.RpcCallMetrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.DownloadFile));
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task DownloadFileShouldGatherMetrics_WhenFails(TentacleType tentacleType, string version)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateFileTransferServiceWith(b => b.BeforeDownloadFile(() => throw exception)).Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "DownloadFile.txt");
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            var downloadFileMetrics = tentacleClientObserver.DownloadFileMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeFailed(downloadFileMetrics, exception);

            tentacleClientObserver.RpcCallMetrics.Should().HaveCountGreaterThan(1); // the first one will be the upload
            var metric = tentacleClientObserver.RpcCallMetrics.Last();
            metric.RpcCallName.Should().Be(nameof(IClientFileTransferService.DownloadFile));
            metric.Succeeded.Should().BeFalse();
        }

        private static void ThenClientOperationMetricsShouldBeSuccessful(ClientOperationMetrics metric)
        {
            metric.Succeeded.Should().BeTrue();
            metric.Exception.Should().BeNull();
            metric.WasCancelled.Should().BeFalse();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);
        }

        private static void ThenClientOperationMetricsShouldBeFailed(ClientOperationMetrics metric, Exception expectedException)
        {
            metric.Succeeded.Should().BeFalse();
            metric.Exception.Should().BeEquivalentTo(expectedException);
            metric.WasCancelled.Should().BeFalse();

            metric.End.Should().BeAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);
        }
    }
}