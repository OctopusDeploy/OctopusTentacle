using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class NugetTentacleFetcher : ITentacleFetcher
    {
        static string DownloadUrlForVersion(string versionString) => $"https://f.feedz.io/octopus-deploy/dependencies/packages/Octopus.Tentacle.CrossPlatformBundle/{versionString}/download";

        public async Task<string> GetTentacleVersion(string downloadPath, string version)
        {
            return await DownloadAndExtractFromUrl(downloadPath, DownloadUrlForVersion(version));
        }

        async Task<string> DownloadAndExtractFromUrl(string directoryPath, string url)
        {
            await Task.CompletedTask;
            var downloadFilePath = Path.Combine(directoryPath, Guid.NewGuid().ToString("N"));

            TestContext.WriteLine($"Downloading {url} to {downloadFilePath}");
            //await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath);

            // For local TODO delte this.
            File.Copy("/tmp/tentaclenuget/Octopus.Tentacle.CrossPlatformBundle.6.3.451.nupkg", downloadFilePath);

            var extractionDirectory = Path.Combine(directoryPath, "extracted");

            ZipFile.ExtractToDirectory(downloadFilePath, extractionDirectory);

            var tentacleArtifact = Path.Combine(extractionDirectory, TentacleArtifactName());

            var tentacleFolder = Path.Combine(directoryPath, "tentacle");
            if (tentacleArtifact.EndsWith(".tar.gz"))
            {
                LinuxTentacleFetcher.ExtractTarGzip(tentacleArtifact, tentacleFolder);
            }
            else
            {
                ZipFile.ExtractToDirectory(tentacleArtifact, tentacleFolder);
            }

            return Path.Combine(tentacleFolder, "tentacle", "Tentacle");
        }

        public string TentacleArtifactName()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                return $"tentacle-{GetFramework()}-win-{Architecture()}.zip";
            }

            if (PlatformDetection.IsRunningOnMac)
            {
                return $"tentacle-{GetFramework()}-osx-{Architecture()}.tar.gz";
            }

            if (PlatformDetection.IsRunningOnNix)
            {
                return $"tentacle-{GetFramework()}-linux-{Architecture()}.tar.gz";
            }

            throw new Exception("Wow this is one fancy OS that tentacle probably can't run on");
        }

        string GetFramework()
        {
            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET 6.0"))
            {
                return "net6.0";
            }

            return string.Concat(RuntimeInformation.FrameworkDescription.Split(' ').Take(2));
        }

        string Architecture()
        {
            return RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        }
    }
}