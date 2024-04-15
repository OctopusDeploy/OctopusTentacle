using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public class LinuxTentacleFetcher : ITentacleFetcher
    {
        private ILogger logger;

        public LinuxTentacleFetcher(ILogger logger)
        {
            this.logger = logger;
        }

        static string LinuxDownloadUrlForVersion(string versionString)
        {
            var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            return $"https://download.octopusdeploy.com/linux-tentacle/tentacle-{versionString}-linux_{architecture}.tar.gz";
        }

        public async Task<string> GetTentacleVersion(string downloadPath, Version version, TentacleRuntime _, CancellationToken cancellationToken)
        {
            var directoryPath = Path.Combine(downloadPath, version.ToString());
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return await DownloadAndExtractFromUrl(directoryPath, LinuxDownloadUrlForVersion(version.ToString()));
        }

        async Task<string> DownloadAndExtractFromUrl(string directoryPath, string url)
        {
            var downloadFilePath = Path.Combine(directoryPath, Guid.NewGuid().ToString("N"));

            logger.Information($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath, logger);

            var extractionDirectory = new DirectoryInfo(Path.Combine(directoryPath, "extracted"));

            ExtractTarGzip(downloadFilePath, extractionDirectory.FullName, logger);
            return Path.Combine(extractionDirectory.FullName, "tentacle", "Tentacle");
        }

        public static void ExtractTarGzip(string gzArchiveName, string destFolder, ILogger logger)
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

            Action<string> log = s => logger.Information(s);
            var exitCode = SilentProcessRunner.ExecuteCommand(
                "tar",
                $"xzvf {gzArchiveName} -C {destFolder}",
                tmp.DirectoryPath,
                log,
                log,
                log,
                CancellationToken.None);

            if (exitCode != 0)
            {
                throw new Exception("Error extracting archive");
            }
        }
    }
}
