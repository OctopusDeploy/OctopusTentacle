﻿using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class FileTransferServiceTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task UploadFileSuccessfully(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var fileToUpload = new RandomTemporaryFileBuilder().Build();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);

            var dataStream = new DataStream(
                fileToUpload.File.Length,
                async (stream, ct) =>
                {
                    using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                    await fileStream.CopyToAsync(stream);
                });

            var uploadResult = await clientAndTentacle.TentacleClient.FileTransferService.UploadFileAsync("the_remote_uploaded_file", dataStream, new(CancellationToken));

            Console.WriteLine($"Source: {fileToUpload.File.FullName}");
            Console.WriteLine($"Destination: {uploadResult.FullPath}");

            var sourceBytes = File.ReadAllBytes(fileToUpload.File.FullName);
            var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);

            sourceBytes.Should().BeEquivalentTo(destinationBytes);
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task DownloadFileSuccessfully(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var fileToDownload = new RandomTemporaryFileBuilder().Build();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);

            var downloadedData = await clientAndTentacle.TentacleClient.FileTransferService.DownloadFileAsync(
                fileToDownload.File.FullName,
                new(CancellationToken));

            var sourceBytes = File.ReadAllBytes(fileToDownload.File.FullName);
            var destinationBytes = await downloadedData.ToBytes(CancellationToken);

            destinationBytes.Should().BeEquivalentTo(sourceBytes);
        }
    }
}
