using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class WindowsOnlyNugetBinsTentacleFetcher : ITentacleFetcher
    {
        static string DownloadUrlForVersion(string versionString) => $"https://f.feedz.io/octopus-deploy/dependencies/packages/Tentacle.Binaries.win-x64/{versionString}/download";
        
        public async Task<string> GetTentacleVersion(string downloadPath, string version)
        {
            var downloadFilePath = Path.Combine(downloadPath, Guid.NewGuid().ToString("N"));

            var url = DownloadUrlForVersion(version);
            TestContext.WriteLine($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath);
            
            var extractionDirectory = Path.Combine(downloadPath, "extracted");
            
            ZipFile.ExtractToDirectory(downloadFilePath, extractionDirectory);

            var buildDir = new DirectoryInfo(Path.Combine(extractionDirectory, "build"));

            var dotnetversionpath = Directory.EnumerateDirectories(buildDir.FullName).FirstOrDefault();
            var tentacelFolder = Directory.EnumerateDirectories(dotnetversionpath).FirstOrDefault();

            return TentacleExeFinder.AddExeExtension(Path.Combine(tentacelFolder, "Tentacle"));


            // netcoreapp2.2

        }
    }
}