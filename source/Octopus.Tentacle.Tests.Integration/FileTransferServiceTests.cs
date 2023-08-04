using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class FileTransferServiceTests : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task UploadFileSuccessfully(TentacleType tentacleType, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var fileToUpload = new RandomTemporaryFileBuilder().Build();

            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

#pragma warning disable CS0612
            var dataStream = new DataStream(
                fileToUpload.File.Length,
                stream =>
                {
                    using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                    fileStream.CopyTo(stream);
                });
#pragma warning restore CS0612

            var uploadResult = await syncOrAsyncHalibut
                .WhenSync(() => clientAndTentacle.TentacleClient.FileTransferService.SyncService.UploadFile("the_remote_uploaded_file", dataStream))
                .WhenAsync(async () => await clientAndTentacle.TentacleClient.FileTransferService.AsyncService.UploadFileAsync(
                    "the_remote_uploaded_file", 
                    dataStream, 
                    new (CancellationToken, null)));

            Console.WriteLine($"Source: {fileToUpload.File.FullName}");
            Console.WriteLine($"Destination: {uploadResult.FullPath}");

            var sourceBytes = File.ReadAllBytes(fileToUpload.File.FullName);
            var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);

            sourceBytes.Should().BeEquivalentTo(destinationBytes);
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task DownloadFileSuccessfully(TentacleType tentacleType, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            using var fileToDownload = new RandomTemporaryFileBuilder().Build();

            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .WithAsyncHalibutFeature(syncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

            var downloadedData = await syncOrAsyncHalibut
                .WhenSync(() => clientAndTentacle.TentacleClient.FileTransferService.SyncService.DownloadFile(fileToDownload.File.FullName))
                .WhenAsync(async () => await clientAndTentacle.TentacleClient.FileTransferService.AsyncService.DownloadFileAsync(
                    fileToDownload.File.FullName, 
                    new(CancellationToken, null)));

            var sourceBytes = File.ReadAllBytes(fileToDownload.File.FullName);
            var destinationBytes = downloadedData.ToBytes();

            destinationBytes.Should().BeEquivalentTo(sourceBytes);
        }
    }
}
