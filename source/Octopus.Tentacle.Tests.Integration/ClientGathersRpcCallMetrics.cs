using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientGathersRpcCallMetrics : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task ExecuteScriptShouldGatherMetrics_WhenSucceeds(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            var (finalResponse, _) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);

            var executeScriptMetrics = tentacleClientObserver.ExecuteScriptMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeSuccessful(executeScriptMetrics);

            string expectedScriptService;
            if (tentacleConfigurationTestCase.Version.HasScriptServiceV3Alpha())
            {
                expectedScriptService = nameof(IScriptServiceV3Alpha);
            }
            else if (tentacleConfigurationTestCase.Version.HasScriptServiceV2())
            {
                expectedScriptService = nameof(IScriptServiceV2);
            }
            else
            {
                expectedScriptService = nameof(IScriptService);
            }

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCall.Name == nameof(ICapabilitiesServiceV2.GetCapabilities) && m.RpcCall.Service == nameof(ICapabilitiesServiceV2));
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCall.Name == nameof(IScriptServiceV2.StartScript) && m.RpcCall.Service == expectedScriptService);
            tentacleClientObserver.RpcCallMetrics.Should().Contain(m => m.RpcCall.Name == nameof(IScriptServiceV2.GetStatus) && m.RpcCall.Service == expectedScriptService);
            tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCall.Name == nameof(IScriptServiceV2.CompleteScript) && m.RpcCall.Service == expectedScriptService);
            tentacleClientObserver.RpcCallMetrics.Should().AllSatisfy(m => m.Succeeded.Should().BeTrue());
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task ExecuteScriptShouldGatherMetrics_WhenFails(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .HookServiceMethod(tentacleConfigurationTestCase, nameof(IAsyncClientScriptServiceV2.StartScriptAsync), (_, _) => throw exception)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            var executeScriptMetrics = tentacleClientObserver.ExecuteScriptMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeFailed(executeScriptMetrics, exception);

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            var startScriptMetric = tentacleClientObserver.RpcCallMetrics.Should().ContainSingle(m => m.RpcCall.Name == "StartScript").Subject;
            startScriptMetric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task UploadFileShouldGatherMetrics_WhenSucceeds(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
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
            metric.RpcCall.Name.Should().Be(nameof(IFileTransferService.UploadFile));
            metric.RpcCall.Service.Should().Be(nameof(IFileTransferService));
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task UploadFileShouldGatherMetrics_WhenFails(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.UploadFileAsync), (_, _) => throw exception)
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act
            await AssertionExtensions.Should(() => clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken)).ThrowAsync<HalibutClientException>();

            // Assert
            var uploadFileMetrics = tentacleClientObserver.UploadFileMetrics.Should().ContainSingle().Subject;
            ThenClientOperationMetricsShouldBeFailed(uploadFileMetrics, exception);

            tentacleClientObserver.RpcCallMetrics.Should().NotBeEmpty();
            var metric = tentacleClientObserver.RpcCallMetrics.Last();
            metric.RpcCall.Name.Should().Be(nameof(IFileTransferService.UploadFile));
            metric.RpcCall.Service.Should().Be(nameof(IFileTransferService));
            metric.Succeeded.Should().BeFalse();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task DownloadFileShouldGatherMetrics_WhenSucceeds(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
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
            metric.RpcCall.Name.Should().Be(nameof(IFileTransferService.DownloadFile));
            metric.RpcCall.Service.Should().Be(nameof(IFileTransferService));
            metric.Succeeded.Should().BeTrue();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task DownloadFileShouldGatherMetrics_WhenFails(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new TestTentacleClientObserver();
            var exception = new HalibutClientException("Error");
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithRetryDuration(TimeSpan.FromSeconds(1))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .HookServiceMethod<IAsyncClientFileTransferService>(nameof(IAsyncClientFileTransferService.DownloadFileAsync), (_, _) => throw exception)
                    .Build())
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
            metric.RpcCall.Name.Should().Be(nameof(IFileTransferService.DownloadFile));
            metric.RpcCall.Service.Should().Be(nameof(IFileTransferService));
            metric.Succeeded.Should().BeFalse();
        }

        private static void ThenClientOperationMetricsShouldBeSuccessful(ClientOperationMetrics metric)
        {
            metric.Succeeded.Should().BeTrue();
            metric.Exception.Should().BeNull();
            metric.WasCancelled.Should().BeFalse();

            metric.End.Should().BeOnOrAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);
        }

        private static void ThenClientOperationMetricsShouldBeFailed(ClientOperationMetrics metric, Exception expectedException)
        {
            metric.Succeeded.Should().BeFalse();
            metric.Exception.Should().BeEquivalentTo(expectedException);
            metric.WasCancelled.Should().BeFalse();

            metric.End.Should().BeOnOrAfter(metric.Start);
            metric.Duration.Should().Be(metric.End - metric.Start);
        }
    }
}