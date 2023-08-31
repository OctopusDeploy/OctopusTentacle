using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class FileTransferServiceTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: false)]
        public async Task UploadFileSuccessfully(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var fileToUpload = new RandomTemporaryFileBuilder().Build();

            await using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleConfigurationTestCase.TentacleType)
                .WithAsyncHalibutFeature(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);
            
            UploadResult uploadResult;

#pragma warning disable CS0612
            if (tentacleConfigurationTestCase.SyncOrAsyncHalibut == SyncOrAsyncHalibut.Sync)
            {
                var dataStream = new DataStream(
                    fileToUpload.File.Length,
                    stream =>
                    {
                        using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                        fileStream.CopyTo(stream);
                    });
#pragma warning restore CS0612

                uploadResult = clientAndTentacle.TentacleClient.FileTransferService.SyncService.UploadFile("the_remote_uploaded_file", dataStream);
            }
            else
            {
                var dataStream = new DataStream(
                    fileToUpload.File.Length,
                    async (stream, ct) =>
                    {
                        using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                        await fileStream.CopyToAsync(stream);
                    });

                uploadResult = await clientAndTentacle.TentacleClient.FileTransferService.AsyncService.UploadFileAsync("the_remote_uploaded_file", dataStream, new(CancellationToken, null));
            }

            Console.WriteLine($"Source: {fileToUpload.File.FullName}");
            Console.WriteLine($"Destination: {uploadResult.FullPath}");

            var sourceBytes = File.ReadAllBytes(fileToUpload.File.FullName);
            var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);

            sourceBytes.Should().BeEquivalentTo(destinationBytes);
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: false)]
        public async Task DownloadFileSuccessfully(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var fileToDownload = new RandomTemporaryFileBuilder().Build();

            await using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleConfigurationTestCase.TentacleType)
                .WithAsyncHalibutFeature(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

            var downloadedData = await tentacleConfigurationTestCase.SyncOrAsyncHalibut
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
