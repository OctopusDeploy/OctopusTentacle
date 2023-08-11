using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientFileTransfersAreNotRetriedWhenRetriesAreDisabled : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task FailedUploadsAreNotRetriedAndFail(TentacleType tentacleType, Version? version, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(version)
                .WithRetriesDisabled()
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
                            // Only kill the connection the first time, causing the upload
                            // to succeed - and therefore failing the test - if retries are attempted
                            if (fileTransferServiceException.UploadLatestException == null)
                            {
                                responseMessageTcpKiller.KillConnectionOnNextResponse();
                            }
                        });
                    })
                    .Build())
                .Build(CancellationToken);

            var remotePath = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "UploadFile.txt");

            var uploadFileTask = clientTentacle.TentacleClient.UploadFile(remotePath, DataStream.FromString("Hello"), CancellationToken);
            
            Func<Task> action = async () => await uploadFileTask;
            await action.Should().ThrowAsync<HalibutClientException>();
            
            fileTransferServiceException.UploadLatestException.Should().NotBeNull();
            fileTransferServiceCallCounts.UploadFileCallCountStarted.Should().Be(1);
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task FailedDownloadsAreNotRetriedAndFail(TentacleType tentacleType, Version? version, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .WithTentacleVersion(version)
                .WithRetriesDisabled()
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
                            // Only kill the connection the first time, causing the download
                            // to succeed - and therefore failing the test - if retries are attempted
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
            var downloadFileTask = clientTentacle.TentacleClient.DownloadFile(remotePath, CancellationToken);

            Func<Task> action = async () => await downloadFileTask;
            await action.Should().ThrowAsync<HalibutClientException>();

            fileTransferServiceException.DownloadFileLatestException.Should().NotBeNull();
            fileTransferServiceCallCounts.DownloadFileCallCountStarted.Should().Be(1);
        }
    }
}
