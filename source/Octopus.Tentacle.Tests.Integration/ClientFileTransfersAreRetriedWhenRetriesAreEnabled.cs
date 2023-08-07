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
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task FailedUploadsAreRetriedAndIsEventuallySuccessful(TentacleType tentacleType, Version? version)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(b =>
                    {
                        b.BeforeUploadFile((service, _, ds) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);
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
        [TestCaseSource(typeof(TentacleTypesAndCommonVersionsToTest))]
        public async Task FailedDownloadsAreRetriedAndIsEventuallySuccessful(TentacleType tentacleType, Version? version)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(version)
                .WithPortForwarderDataLogging()
                .WithResponseMessageTcpKiller(out var responseMessageTcpKiller)
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .LogCallsToFileTransferService()
                    .CountCallsToFileTransferService(out var fileTransferServiceCallCounts)
                    .RecordExceptionThrownInFileTransferService(out var fileTransferServiceException)
                    .DecorateFileTransferServiceWith(b =>
                    {
                        b.BeforeDownloadFile((service, _) =>
                        {
                            service.EnsureTentacleIsConnectedToServer(Logger);
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
