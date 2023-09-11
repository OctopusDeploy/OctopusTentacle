using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public class WindowsOnlyNugetBinsTentacleFetcher : ITentacleFetcher
    {
        static string DownloadUrlForVersion(string versionString) => $"https://f.feedz.io/octopus-deploy/dependencies/packages/Tentacle.Binaries.win-x64/{versionString}/download";

        private ILogger logger;

        public WindowsOnlyNugetBinsTentacleFetcher(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<string> GetTentacleVersion(string downloadPath, Version version, TentacleRuntime _, CancellationToken cancellationToken)
        {
            var downloadFilePath = Path.Combine(downloadPath, Guid.NewGuid().ToString("N"));

            var url = DownloadUrlForVersion(version.ToString());
            logger.Information($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath, logger);
            
            var extractionDirectory = Path.Combine(downloadPath, "extracted");
            
            ZipFile.ExtractToDirectory(downloadFilePath, extractionDirectory);

            var buildDir = new DirectoryInfo(Path.Combine(extractionDirectory, "build"));

            var dotNetVersionPath = Directory.EnumerateDirectories(buildDir.FullName).FirstOrDefault();
            var tentacleFolder = Directory.EnumerateDirectories(dotNetVersionPath).FirstOrDefault();

            return TentacleExeFinder.GetExecutablePath(tentacleFolder);
        }
    }
}
