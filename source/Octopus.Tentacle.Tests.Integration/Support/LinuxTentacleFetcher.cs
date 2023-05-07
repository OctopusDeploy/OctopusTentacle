using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class LinuxTentacleFetcher : ITentacleFetcher
    {
        static string LinuxDownloadUrlForVersion(string versionString) => $"https://download.octopusdeploy.com/linux-tentacle/tentacle-{versionString}-linux_x64.tar.gz";

        public async Task<string> GetTentacleVersion(string downloadPath, string version)
        {
            var directoryPath = Path.Combine(downloadPath, version);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return await DownloadAndExtractFromUrl(directoryPath, LinuxDownloadUrlForVersion(version));
        }

        static async Task<string> DownloadAndExtractFromUrl(string directoryPath, string url)
        {
            var downloadFilePath = Path.Combine(directoryPath, Guid.NewGuid().ToString("N"));

            TestContext.WriteLine($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath);

            var extractionDirectory = new DirectoryInfo(Path.Combine(directoryPath, "extracted"));

            ExtractTarGzip(downloadFilePath, extractionDirectory.FullName);
            return Path.Combine(extractionDirectory.FullName, "tentacle", "Tentacle");
        }

        public static void ExtractTarGzip(string gzArchiveName, string destFolder)
        {
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            // We need to use tar directly rather than a C# implementation
            // All C# implementations that we have tried here have not been able to preserve the file permissions stored in the archive
            // In practice, this means that after extraction the executables don't have the executable bit.
            // Falling back to good old fashioned `tar` does the job nicely :)
            using var tmp = new TemporaryDirectory();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                "tar",
                $"xzvf {gzArchiveName} -C {destFolder}",
                tmp.DirectoryPath,
                TestContext.WriteLine,
                TestContext.WriteLine,
                TestContext.WriteLine,
                CancellationToken.None);

            if (exitCode != 0)
            {
                throw new Exception("Error extracting archive");
            }
        }
    }
}