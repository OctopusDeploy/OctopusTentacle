using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientFileTransfersAreRetriedWhenRetriesAreEnabled : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task FailedUploadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordCallMetricsToService<IClientFileTransferService, IAsyncClientFileTransferService>(out var serviceMetrics)
                    .RegisterInvocationHooks<IClientFileTransferService>(async _ =>
                    {
                        await tcpConnectionUtilities.RestartTcpConnection();

                        if (serviceMetrics.LatestException(nameof(IClientFileTransferService.UploadFile)) == null)
                        {
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        }
                    }, nameof(IClientFileTransferService.UploadFile))
                    .RegisterInvocationHooks<IAsyncClientFileTransferService>(async _ =>
                    {
                        await tcpConnectionUtilities.RestartTcpConnection();

                        if (serviceMetrics.LatestException(nameof(IAsyncClientFileTransferService.UploadFileAsync)) == null)
                        {
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        }
                    }, nameof(IAsyncClientFileTransferService.UploadFileAsync))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            var res = await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken, inMemoryLog);
            res.Length.Should().Be(5);

            serviceMetrics.LatestException(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Should().NotBeNull();
            serviceMetrics.StartedCount(nameof(IAsyncClientFileTransferService.UploadFileAsync)).Should().Be(2);

            var actuallySent = (await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken)).GetUtf8String();
            actuallySent.Should().Be("Hello");

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task FailedDownloadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTcpConnectionUtilities(Logger, out var tcpConnectionUtilities)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordCallMetricsToService<IClientFileTransferService, IAsyncClientFileTransferService>(out var serviceMetrics)
                    .RegisterInvocationHooks<IClientFileTransferService>(async _ =>
                    {
                        await tcpConnectionUtilities.RestartTcpConnection();

                        if (serviceMetrics.LatestException(nameof(IClientFileTransferService.DownloadFile)) == null)
                        {
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        }
                    }, nameof(IClientFileTransferService.DownloadFile))
                    .RegisterInvocationHooks<IAsyncClientFileTransferService>(async _ =>
                    {
                        await tcpConnectionUtilities.RestartTcpConnection();

                        if (serviceMetrics.LatestException(nameof(IAsyncClientFileTransferService.DownloadFileAsync)) == null)
                        {
                            responseMessageTcpKiller.KillConnectionOnNextResponse();
                        }
                    }, nameof(IAsyncClientFileTransferService.DownloadFileAsync))
                    .Build())
                .Build(CancellationToken);

            var inMemoryLog = new InMemoryLog();

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            var actuallySent = (await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken, inMemoryLog)).GetUtf8String();

            serviceMetrics.LatestException(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Should().NotBeNull();
            serviceMetrics.StartedCount(nameof(IAsyncClientFileTransferService.DownloadFileAsync)).Should().Be(2);

            actuallySent.Should().Be("Hello");

            inMemoryLog.ShouldHaveLoggedRetryAttemptsAndNoRetryFailures();
        }
    }
}
