using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
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
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(b =>
                    {
                        b.BeforeUploadFile(async (service, _, ds) =>
                        {
                            await service.EnsureTentacleIsConnectedToServer(Logger);
                            if (fileTransferServiceException.UploadLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        });
                    })
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            var res = await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            res.Length.Should().Be(5);
            fileTransferServiceException.UploadLatestException.Should().NotBeNull();
            fileTransferServiceCallCounts.UploadFileCallCountStarted.Should().Be(2);

            var actuallySent = (await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken)).GetUtf8String();
            actuallySent.Should().Be("Hello");
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task FailedDownloadsAreRetriedAndIsEventuallySuccessful(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(b =>
                    {
                        b.BeforeDownloadFile(async (service, _) =>
                        {
                            await service.EnsureTentacleIsConnectedToServer(Logger);
                            if (fileTransferServiceException.DownloadFileLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        });
                    })
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            await clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            var actuallySent = (await clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken)).GetUtf8String();

            fileTransferServiceException.DownloadFileLatestException.Should().NotBeNull();
            fileTransferServiceCallCounts.DownloadFileCallCountStarted.Should().Be(2);
            actuallySent.Should().Be("Hello");
        }
    }
}
