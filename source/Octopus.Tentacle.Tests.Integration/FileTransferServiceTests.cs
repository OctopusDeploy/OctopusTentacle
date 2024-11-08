using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Client.Extensions;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
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
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            Logger.Information($"UploadFileSuccessfully Available Disk space before starting: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            using var fileToUpload = new RandomTemporaryFileBuilder().Build();
            Logger.Information($"UploadFileSuccessfully Available Disk space before CreateLegacyBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);

            Logger.Information($"UploadFileSuccessfully Available Disk space after CreateLegacyBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var dataStream = new DataStream(
                fileToUpload.File.Length,
                async (stream, ct) =>
                {
                    using var fileStream = File.OpenRead(fileToUpload.File.FullName);
                    await fileStream.CopyToAsync(stream);
                });
            
            Logger.Information($"UploadFileSuccessfully Available Disk after creating a DataStream: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var uploadResult = await clientAndTentacle.TentacleClient.FileTransferService.UploadFileAsync("the_remote_uploaded_file", dataStream, new(CancellationToken));
            
            Logger.Information($"UploadFileSuccessfully Available Disk space after UploadFileAsync: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            Console.WriteLine($"Source: {fileToUpload.File.FullName}");
            Console.WriteLine($"Destination: {uploadResult.FullPath}");

            var sourceBytes = File.ReadAllBytes(fileToUpload.File.FullName);
            var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);
            
            Logger.Information($"UploadFileSuccessfully Available Disk space after assertion: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            sourceBytes.Should().BeEquivalentTo(destinationBytes);
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task DownloadFileSuccessfully(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {            
            var driveInfos = DriveInfo.GetDrives().Where(d => d.IsReady);

            using var fileToDownload = new RandomTemporaryFileBuilder().Build();
            Logger.Information($"DownloadFileSuccessfully Available Disk space before RandomTemporaryFileBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateLegacyBuilder().Build(CancellationToken);
            Logger.Information($"DownloadFileSuccessfully Available Disk space after CreateLegacyBuilder: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var downloadedData = await clientAndTentacle.TentacleClient.FileTransferService.DownloadFileAsync(
                fileToDownload.File.FullName,
                new(CancellationToken));
            Logger.Information($"DownloadFileSuccessfully Available Disk space after DownloadFileAsync: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            var sourceBytes = File.ReadAllBytes(fileToDownload.File.FullName);
            var destinationBytes = await downloadedData.ToBytes(CancellationToken);
            Logger.Information($"DownloadFileSuccessfully Available Disk space after downloadedData: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

            destinationBytes.Should().BeEquivalentTo(sourceBytes);
            Logger.Information($"DownloadFileSuccessfully Available Disk space after assertion: {driveInfos.Select(d => $"{d.Name}: {d.AvailableFreeSpace}").ToList().StringJoin(", ")}");

        }
    }
}
