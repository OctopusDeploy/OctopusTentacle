using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class TentacleClientObserver : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task AnErrorDuringTheCallbackTo_ExecuteScriptCompleted_ShouldNotThrowOrStopScriptExecution(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new BrokenTentacleClientObserver(errorOnExecuteScriptCompleted: true);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            // We should have completed the script and not failed due to the error thrown by the TentacleClientObserver
            recordedUsages.For(nameof(IAsyncScriptServiceV3Alpha.CompleteScriptAsync)).Started.Should().Be(1);
        }

        [Test]
        [TentacleConfigurations]
        public async Task AnErrorDuringTheCallbackTo_RpcCallComplete_ShouldNotThrowOrStopScriptExecution(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new BrokenTentacleClientObserver(errorOnRpcCallCompleted: true);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(b => b.Print("Hello"))
                .Build();

            // Act
            await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            // Assert
            // We should have completed the script and not failed due to the error thrown by the TentacleClientObserver
            recordedUsages.For(nameof(IAsyncScriptServiceV3Alpha.CompleteScriptAsync)).Started.Should().Be(1);
        }

        [Test]
        [TentacleConfigurations]
        public async Task AnErrorDuringTheCallbackTo_UploadFileCompleted_ShouldNotThrowAnExecution(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new BrokenTentacleClientObserver(errorOnUploadFileCompleted: true);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            // Act + Assert
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
        }

        [Test]
        [TentacleConfigurations]
        public async Task AnErrorDuringTheCallbackTo_DownloadFileCompleted_ShouldNotThrowAnExecution(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Arrange
            var tentacleClientObserver = new BrokenTentacleClientObserver(errorOnDownloadFileCompleted: true);

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleClientObserver(tentacleClientObserver)
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "DownloadFile.txt");
            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);

            // Act + Assert
            await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);
        }

        class BrokenTentacleClientObserver : ITentacleClientObserver
        {
            private readonly bool errorOnRpcCallCompleted;
            private readonly bool errorOnUploadFileCompleted;
            private readonly bool errorOnDownloadFileCompleted;
            private readonly bool errorOnExecuteScriptCompleted;

            public BrokenTentacleClientObserver(
                bool errorOnRpcCallCompleted = false,
                bool errorOnUploadFileCompleted = false,
                bool errorOnDownloadFileCompleted = false,
                bool errorOnExecuteScriptCompleted = false)
            {
                this.errorOnRpcCallCompleted = errorOnRpcCallCompleted;
                this.errorOnUploadFileCompleted = errorOnUploadFileCompleted;
                this.errorOnDownloadFileCompleted = errorOnDownloadFileCompleted;
                this.errorOnExecuteScriptCompleted = errorOnExecuteScriptCompleted;
            }

            public void RpcCallCompleted(RpcCallMetrics metrics, ILog logger)
            {
                if (errorOnRpcCallCompleted)
                {
                    throw new Exception($"RpcCallCompleted {Guid.NewGuid()}");
                }
            }

            public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
            {
                if (errorOnUploadFileCompleted)
                {
                    throw new Exception($"UploadFileCompleted {Guid.NewGuid()}");
                }
            }

            public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
            {
                if (errorOnDownloadFileCompleted)
                {
                    throw new Exception($"DownloadFileCompleted {Guid.NewGuid()}");
                }
            }

            public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
            {
                if (errorOnExecuteScriptCompleted)
                {
                    throw new Exception($"ExecuteScriptCompleted {Guid.NewGuid()}");
                }
            }
        }
    }
}
