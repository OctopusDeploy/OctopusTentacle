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
    [RunTestsInParallelLocallyIfEnabledButNeverOnTeamCity]
    // [IntegrationTestTimeout]
    public class FileTransferServiceTests : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task UploadFileSuccessfully(TentacleType tentacleType)
        {
            using var fileToUpload = new RandomTemporaryFileBuilder().Build();

            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .Build(CancellationToken);

            var uploadResult = clientAndTentacle.TentacleClient.FileTransferService.UploadFile(
                "the_remote_uploaded_file",
                new DataStream(
                    fileToUpload.File.Length,
                    stream =>
                    {
                        using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                        fileStream.CopyTo(stream);
                    }));

            Console.WriteLine($"Source: {fileToUpload.File.FullName}");
            Console.WriteLine($"Destination: {uploadResult.FullPath}");

            var sourceBytes = File.ReadAllBytes(fileToUpload.File.FullName);
            var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);

            sourceBytes.Should().BeEquivalentTo(destinationBytes);
        }

        [Test]
        [TestCaseSource(typeof(TentacleTypesToTest))]
        public async Task DownloadFileSuccessfully(TentacleType tentacleType)
        {
            using var fileToDownload = new RandomTemporaryFileBuilder().Build();

            using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleType)
                .Build(CancellationToken);

            var downloadedData = clientAndTentacle.TentacleClient.FileTransferService.DownloadFile(fileToDownload.File.FullName);

            var sourceBytes = File.ReadAllBytes(fileToDownload.File.FullName);
            var destinationBytes = downloadedData.ToBytes();

            destinationBytes.Should().BeEquivalentTo(sourceBytes);
        }
    }
}
