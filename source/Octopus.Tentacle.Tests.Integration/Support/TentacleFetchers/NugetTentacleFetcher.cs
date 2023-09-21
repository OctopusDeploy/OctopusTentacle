using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    /// <summary>
    /// Downloads tentacle from the cross platform nuget package
    /// Earliest version supported: 6.0.174
    /// </summary>
    public class NugetTentacleFetcher : ITentacleFetcher
    {
        private ILogger logger;

        public NugetTentacleFetcher(ILogger logger)
        {
            this.logger = logger;
        }

        static string DownloadUrlForVersion(string versionString) => $"https://f.feedz.io/octopus-deploy/dependencies/packages/Octopus.Tentacle.CrossPlatformBundle/{versionString}/download";

        public async Task<string> GetTentacleVersion(string downloadPath, Version version, TentacleRuntime runtime, CancellationToken cancellationToken)
        {
            return await DownloadAndExtractFromUrl(downloadPath, runtime, DownloadUrlForVersion(version.ToString()));
        }

        async Task<string> DownloadAndExtractFromUrl(string directoryPath, TentacleRuntime runtime, string url)
        {
            await Task.CompletedTask;
            var downloadFilePath = Path.Combine(directoryPath, Guid.NewGuid().ToString("N"));

            logger.Information($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath, logger);

            var extractionDirectory = Path.Combine(directoryPath, "extracted");

            ZipFile.ExtractToDirectory(downloadFilePath, extractionDirectory);

            // This is the path to the runtime-specific artifact
            var tentacleArtifact = Path.Combine(extractionDirectory, TentacleArtifactName(runtime));

            var tentacleFolder = Path.Combine(directoryPath, "tentacle");
            if (tentacleArtifact.EndsWith(".tar.gz"))
            {
                LinuxTentacleFetcher.ExtractTarGzip(tentacleArtifact, tentacleFolder, logger);
            }
            else
            {
                ZipFile.ExtractToDirectory(tentacleArtifact, tentacleFolder);
            }

            return Path.Combine(tentacleFolder, "tentacle", "Tentacle");
        }

        public string TentacleArtifactName(TentacleRuntime runtime)
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                var net48ArtifactName = "tentacle-net48-win.zip";
                var net60ArtifactName = $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-win-{Architecture()}.zip";

                var name = runtime switch
                {
                    TentacleRuntime.DotNet6 => net60ArtifactName,
                    TentacleRuntime.Framework48 => net48ArtifactName,
                    _ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, null)
                };

                return name;
            }

            if (PlatformDetection.IsRunningOnMac)
            {
                return $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-osx-{Architecture()}.tar.gz";
            }

            if (PlatformDetection.IsRunningOnNix)
            {
                return $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-linux-{Architecture()}.tar.gz";
            }

            throw new Exception("Wow this is one fancy OS that tentacle probably can't run on");
        }

        string Architecture()
        {
            return RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        }
    }
}
