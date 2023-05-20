using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration.Services.FileTransfer
{
    public class FileTransferTestsWithPollingTentacle
    {
        [Test]
        public async Task UploadFileSuccessfully()
        {
            FileInfo fileToUpload = null;

            try
            {
                fileToUpload = GenerateRandomFile();

                await RunWithTentacleClientForPollingTentacle(client =>
                {
                    var uploadResult = client.FileTransferService.UploadFile(
                        "the_remote_uploaded_file",
                        new DataStream(
                            fileToUpload.Length,
                            stream =>
                            {
                                using var fileStream = File.OpenRead(fileToUpload.FullName);
                                fileStream.CopyTo(stream);
                            }));

                    Console.WriteLine($"Source: {fileToUpload.FullName}");
                    Console.WriteLine($"Destination: {uploadResult.FullPath}");

                    var sourceBytes = File.ReadAllBytes(fileToUpload.FullName);
                    var destinationBytes = File.ReadAllBytes(uploadResult.FullPath);

                    sourceBytes.Should().BeEquivalentTo(destinationBytes);
                });
            }
            finally
            {
                if (fileToUpload != null)
                {
                    File.Delete(fileToUpload.FullName);
                }
            }
        }

        async Task RunWithTentacleClientForPollingTentacle(Action<TentacleClient.TentacleClient> clientAction)
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                clientAction(tentacleClient);
            }
        }

        FileInfo GenerateRandomFile()
        {
            var tempFile = Path.GetTempFileName();
            // 2 MB
            byte[] data = new byte[2 * 1024 * 1024];
            Random rng = new Random();
            rng.NextBytes(data);
            File.WriteAllBytes(tempFile, data);
            return new FileInfo(tempFile);
        }
    }
}
